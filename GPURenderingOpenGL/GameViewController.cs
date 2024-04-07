using System;
using System.Diagnostics;

using CoreGraphics;
using Foundation;
using GLKit;
using OpenGLES;
using OpenTK;
using OpenTK.Graphics.ES20;
using UIKit;

namespace GPURenderingOpenGL
{
    [Register("GameViewController")]
    public class GameViewController : GLKViewController, IGLKViewDelegate
    {
        #region 定数データ
        /// <summary>
        /// テクスチャ幅（固定）
        /// </summary>
        private static readonly int TextureWidth = 1184;
        /// <summary>
        /// テクスチャ高さ（固定）
        /// </summary>
        private static readonly int TextureHeight = 740;

        /// <summary>
        /// 四角形のための頂点座標データ（固定）
        /// </summary>
        private static readonly float[] vertexData = {
			// positionX, positionY, positionZ, textureX, textureY
            -TextureWidth / 2, -TextureHeight / 2,  0.0f, 0.0f, 1.0f, // 左下
            -TextureWidth / 2,  TextureHeight / 2,  0.0f, 0.0f, 0.0f, // 左上
             TextureWidth / 2, -TextureHeight / 2,  0.0f, 1.0f, 1.0f, // 右下
             TextureWidth / 2,  TextureHeight / 2,  0.0f, 1.0f, 0.0f  // 右上
        };
        #endregion

        /// <summary>
        /// 頂点データ番号を定義します。
        /// </summary>
        private enum VertexAttribute
        {
            Position = 0,       // 頂点位置
            TextureCoordinate   // テクスチャ座標
        }

        /// <summary>
        /// シェーダに送るデータ種類を定義します。
        /// </summary>
        private enum Uniform
        {
            ViewportSize = 0,   // ビューポートサイズ
            ModelMatrix,        // モデル行列
            ViewMatrix,         // ビュー行列
            Count
        }

        #region メンバ変数
        /// <summary>
        /// シェーダプログラム
        /// </summary>
        private int program;

        /// <summary>
        /// 頂点配列オブジェクト
        /// </summary>
        private uint vertexArray;
        /// <summary>
        /// 頂点バッファオブジェクト
        /// </summary>
        private uint vertexBuffer;

        /// <summary>
        /// ユニフォーム番号を保持します。
        /// </summary>
        private int[] uniforms = new int[(int)Uniform.Count];

        /// <summary>
        /// テクスチャ
        /// </summary>
        private GLKTextureInfo textureInfo;

        /// <summary>
        /// モデル行列
        /// </summary>
        private Matrix4 modelMatrix;
        /// <summary>
        /// ビュー行列
        /// </summary>
        private Matrix4 viewMatrix;

        /// <summary>
        /// ビューポートサイズ
        /// </summary>
        private Vector2i viewportSize;

        /// <summary>
        /// 移動量
        /// </summary>
        private CGPoint move = CGPoint.Empty;
        private CGPoint oldMoved = CGPoint.Empty;

        /// <summary>
        /// 拡大率
        /// </summary>
        private nfloat scale = 1.0f;
        private nfloat oldScaled = 1.0f;

        /// <summary>
        /// 回転量
        /// </summary>
        private nfloat rotate = 0.0f;
        private nfloat oldRotated = 0.0f;
        #endregion

        #region プロパティ
        /// <summary>
        /// OpenGL ESのコンテキスト
        /// </summary>
        private EAGLContext Context { get; set; }
        #endregion

        [Export("initWithCoder:")]
        public GameViewController(NSCoder coder) : base(coder)
        {
        }

        #region ViewController
        /// <summary>
        /// コントローラーのビューがメモリにロードされた後に呼び出されます。
        /// </summary>
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View.Opaque = true;
            View.BackgroundColor = null;
            View.ContentScaleFactor = UIScreen.MainScreen.Scale;

            // パンジェスチャー追加
            View.AddGestureRecognizer(new UIPanGestureRecognizer((UIPanGestureRecognizer sender) =>
            {
                if (sender.State == UIGestureRecognizerState.Began)
                {
                    oldMoved = move;
                }
                CGPoint tmpMove = sender.TranslationInView(View);
                move = new CGPoint(oldMoved.X + tmpMove.X, oldMoved.Y - tmpMove.Y);
            }));
            // ピンチジェスチャー追加
            View.AddGestureRecognizer(new UIPinchGestureRecognizer((UIPinchGestureRecognizer sender) =>
            {
                if (sender.State == UIGestureRecognizerState.Began)
                {
                    oldScaled = scale;
                }
                nfloat tmpScale = sender.Scale;
                scale = oldScaled * tmpScale;
            }));
            // ローテーションジェスチャー追加
            View.AddGestureRecognizer(new UIRotationGestureRecognizer((UIRotationGestureRecognizer sender) =>
            {
                if (sender.State == UIGestureRecognizerState.Began)
                {
                    oldRotated = rotate;
                }
                nfloat tmpRotation = sender.Rotation;
                rotate = oldRotated + tmpRotation;
            }));

            SetupGL();
        }

        /// <summary>
        /// リソースの解放を行います。
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            TearDownGL();

            if (EAGLContext.CurrentContext == Context)
                EAGLContext.SetCurrentContext(null);
        }

        /// <summary>
        /// アプリがメモリ警告を受信すると、呼び出されます。
        /// </summary>
        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();

            if (IsViewLoaded && View.Window == null)
            {
                View = null;

                TearDownGL();

                if (EAGLContext.CurrentContext == Context)
                {
                    EAGLContext.SetCurrentContext(null);
                }
            }

            // Dispose of any resources that can be recreated.
        }

        /// <summary>
        /// View Controllerがステータスバーを非表示にするか表示するかを指定します。
        /// </summary>
        /// <returns></returns>
        public override bool PrefersStatusBarHidden()
        {
            return true;
        }
        #endregion

        #region OpenGL
        /// <summary>
        /// OpenGLのセットアップを行います。
        /// </summary>
        private void SetupGL()
        {
            // コンテキストの作成
            Context = new EAGLContext(EAGLRenderingAPI.OpenGLES2);

            if (Context == null)
            {
                Debug.WriteLine("Failed to create ES context");
            }

            // Viewの設定
            GLKView view = (GLKView)View;
            view.Context = Context;

            // 使用するコンテキストの指定
            EAGLContext.SetCurrentContext(Context);

            // シェーダのロード
            LoadShaders();

            // テクスチャのロード
            LoadTexture();

            // 1つの頂点配列オブジェクトの生成
            GL.Oes.GenVertexArrays(1, out vertexArray);
            // 頂点配列オブジェクトの指定
            GL.Oes.BindVertexArray(vertexArray);

            // 1つの頂点バッファオブジェクトの生成
            GL.GenBuffers(1, out vertexBuffer);
            // 頂点バッファオブジェクトの指定
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
            // 頂点バッファオブジェクトに頂点データを渡す
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertexData.Length * sizeof(float)), vertexData, BufferUsage.StaticDraw);

            // 頂点データのデータ構造を指定
            GL.EnableVertexAttribArray((int)VertexAttribute.Position);
            GL.VertexAttribPointer((int)VertexAttribute.Position, 3, VertexAttribPointerType.Float, false, sizeof(float) * 5, new IntPtr(0));
            GL.EnableVertexAttribArray((int)VertexAttribute.TextureCoordinate);
            GL.VertexAttribPointer((int)VertexAttribute.TextureCoordinate, 2, VertexAttribPointerType.Float, false, sizeof(float) * 5, new IntPtr(3 * sizeof(float)));

            // 頂点配列オブジェクトの指定解除
            GL.Oes.BindVertexArray(0);
        }

        /// <summary>
        /// OpenGLのリソースを解放します。
        /// </summary>
        private void TearDownGL()
        {
            // 使用するコンテキストの指定
            EAGLContext.SetCurrentContext(Context);

            // 頂点バッファオブジェクトの削除
            GL.DeleteBuffers(1, ref vertexBuffer);
            // 頂点配列オブジェクトの削除
            GL.Oes.DeleteVertexArrays(1, ref vertexArray);

            // シェーダの削除
            if (program > 0)
            {
                GL.DeleteProgram(program);
                program = 0;
            }

            // テクスチャの削除
            GL.DeleteTexture(textureInfo.Name);
        }

        /// <summary>
        /// テクスチャをロードします。
        /// </summary>
        /// <returns></returns>
        private bool LoadTexture()
        {
            string path = NSBundle.MainBundle.PathForResource("apple-evolution-thumbnail", "jpg");

            NSError error;
            GLKTextureOperations operations = new GLKTextureOperations();
            textureInfo = GLKTextureLoader.FromFile(path, operations, out error);

            if (error != null)
            {
                Console.WriteLine(error);
                return false;
            }

            return textureInfo != null;
        }

        /// <summary>
        /// シェーダをロードします。
        /// </summary>
        /// <returns></returns>
        private bool LoadShaders()
        {
            int vertShader, fragShader;

            // シェーダプログラムの作成
            program = GL.CreateProgram();

            // 頂点シェーダを作成
            if (!CompileShader(ShaderType.VertexShader, LoadResource("Shader", "vsh"), out vertShader))
            {
                Console.WriteLine("Failed to compile vertex shader");
                return false;
            }
            // フラグメントシェーダを作成
            if (!CompileShader(ShaderType.FragmentShader, LoadResource("Shader", "fsh"), out fragShader))
            {
                Console.WriteLine("Failed to compile fragment shader");
                return false;
            }

            // シェーダプログラムに頂点シェーダを登録する
            GL.AttachShader(program, vertShader);

            // シェーダプログラムにフラグメントシェーダを登録する
            GL.AttachShader(program, fragShader);

            // 頂点データの番号を指定
            // プログラムのリンク前に行う必要がある
            GL.BindAttribLocation(program, (int)VertexAttribute.Position, "position");
            GL.BindAttribLocation(program, (int)VertexAttribute.TextureCoordinate, "texCoordinate");

            // プログラムをリンクする
            if (!LinkProgram(program))
            {
                Console.WriteLine("Failed to link program: {0:x}", program);

                if (vertShader != 0)
                    GL.DeleteShader(vertShader);

                if (fragShader != 0)
                    GL.DeleteShader(fragShader);

                if (program != 0)
                {
                    GL.DeleteProgram(program);
                    program = 0;
                }
                return false;
            }

            // ユニフォーム番号の取得
            uniforms[(int)Uniform.ViewportSize] = GL.GetUniformLocation(program, "viewportSize");
            uniforms[(int)Uniform.ModelMatrix] = GL.GetUniformLocation(program, "modelMatrix");
            uniforms[(int)Uniform.ViewMatrix] = GL.GetUniformLocation(program, "viewMatrix");

            // 一時オブジェクトの解放
            if (vertShader != 0)
            {
                GL.DetachShader(program, vertShader);
                GL.DeleteShader(vertShader);
            }
            if (fragShader != 0)
            {
                GL.DetachShader(program, fragShader);
                GL.DeleteShader(fragShader);
            }

            return true;
        }

        /// <summary>
        /// シェーダをコンパイルします。
        /// </summary>
        /// <param name="type">シェーダタイプ</param>
        /// <param name="src">コード</param>
        /// <param name="shader">シェーダ番号</param>
        /// <returns></returns>
        private bool CompileShader(ShaderType type, string src, out int shader)
        {
            // シェーダ作成後、コンパイル
            shader = GL.CreateShader(type);
            GL.ShaderSource(shader, src);
            GL.CompileShader(shader);

#if DEBUG
            int logLength = 0;
            GL.GetShader(shader, ShaderParameter.InfoLogLength, out logLength);
            if (logLength > 0)
            {
                Console.WriteLine("Shader compile log:\n{0}", GL.GetShaderInfoLog(shader));
            }
#endif

            // シェーダ番号取得
            int status = 0;
            GL.GetShader(shader, ShaderParameter.CompileStatus, out status);
            if (status == 0)
            {
                GL.DeleteShader(shader);
                return false;
            }

            return true;
        }

        /// <summary>
        /// プログラムをリンクします。
        /// </summary>
        /// <param name="prog">プログラム番号</param>
        /// <returns></returns>
        private bool LinkProgram(int prog)
        {
            GL.LinkProgram(prog);

#if DEBUG
            int logLength = 0;
            GL.GetProgram(prog, ProgramParameter.InfoLogLength, out logLength);
            if (logLength > 0)
                Console.WriteLine("Program link log:\n{0}", GL.GetProgramInfoLog(prog));
#endif
            int status = 0;
            GL.GetProgram(prog, ProgramParameter.LinkStatus, out status);
            return status != 0;
        }

        /// <summary>
        /// 各フレームが表示される前に呼び出されます。
        /// </summary>
        public override void Update()
        {
            viewportSize = new Vector2i((int)(View.Bounds.Size.Width * UIScreen.MainScreen.Scale), (int)(View.Bounds.Size.Height * UIScreen.MainScreen.Scale));

            Matrix4 translationMatrix = Matrix4.CreateTranslation(0.0f, 0.0f, 0.0f);
            // UIRotationGestureRecognizerは時計回りが正
            // CreateRotationZは反時計回りが正
            Matrix4 rotationMatrix = Matrix4.CreateRotationZ((float)-rotate);
            Matrix4 scaleMatrix = Matrix4.Scale((float)scale, (float)scale, 1.0f);

            modelMatrix = translationMatrix * rotationMatrix * scaleMatrix;

            viewMatrix = Matrix4.CreateTranslation((float)move.X, (float)move.Y, 0.0f);
        }

        /// <summary>
        /// 描画処理を行います。
        /// </summary>
        /// <param name="view">コンテンツの再描画を要求するビュー</param>
        /// <param name="rect">描画領域</param>
        void IGLKViewDelegate.DrawInRect(GLKView view, CoreGraphics.CGRect rect)
        {
            // カラーを設定して、クリア
            GL.ClearColor(0.65f, 0.65f, 0.65f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // 頂点配列オブジェクトを指定
            GL.Oes.BindVertexArray(vertexArray);

            // シェーダプログラムを指定
            GL.UseProgram(program);

            // テクスチャを指定
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureInfo.Name);

            // 頂点データ以外に描画に必要な情報を設定する
            GL.UniformMatrix4(uniforms[(int)Uniform.ModelMatrix], false, ref modelMatrix);
            GL.UniformMatrix4(uniforms[(int)Uniform.ViewMatrix], false, ref viewMatrix);
            GL.Uniform2(uniforms[(int)Uniform.ViewportSize], viewportSize.X, viewportSize.Y);

            // 描画を行う
            GL.DrawArrays(BeginMode.TriangleStrip, 0, 4);
        }
        #endregion

        /// <summary>
        /// リソースを取得します。
        /// </summary>
        /// <param name="name">リソース名</param>
        /// <param name="type">リソース型</param>
        /// <returns></returns>
        public static string LoadResource(string name, string type)
        {
            string path = NSBundle.MainBundle.PathForResource(name, type);
            return System.IO.File.ReadAllText(path);
        }
    }
}

