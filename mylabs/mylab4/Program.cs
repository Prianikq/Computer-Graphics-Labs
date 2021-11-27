using Device     = CGLabPlatform.OGLDevice;
using DeviceArgs = CGLabPlatform.OGLDeviceUpdateArgs;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SharpGL;
using CGLabPlatform;
// ==================================================================================


using CGApplication = MyApp;
using System.ComponentModel;

public abstract class MyApp : OGLApplicationTemplate<MyApp>
{
    #region Классы
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public Vertex(double px, double py, double pz, double pw, byte pr, byte pg, byte pb)
        {
            vx = px;
            vy = py;
            vz = pz;
            vw = pw;
            r = pr;
            g = pg;
            b = pb;
        }
        public readonly double vx, vy, vz, vw;
        public readonly byte r, g, b;
    }
    #endregion


    // TODO: Добавить свойства, поля

    [Flags]
    private enum Commands : int
    {
        None = 0,
        Transform = 1 << 0,
        FigureChange = 1 << 1,
        NewFigure = 1 << 3,
        ChangeProjectionMatrix = 1 << 4
    }

    #region Свойства
    private DMatrix4 _PointTransform;
    private Commands _Commands = Commands.FigureChange;
    private DVector3 _Rotations;
    private DVector3 _Offset;
    private DVector3 _Scale;

    [DisplayNumericProperty(Default: new[] { 1d, 0, 0, 0,
                                             0, 1d, 0, 0,
                                             0, 0, 1d, 0,
                                             0, 0, 0, 1d }, 0.01, 4, null)]
    public DMatrix4 PointTransform
    {
        get { return _PointTransform; }
        set
        {
            if (value == _PointTransform)
                return;
            _PointTransform = value;
            _Commands |= Commands.Transform;
            OnPropertyChanged();
        }
    }

    [DisplayNumericProperty(Default: new[] { 2d, 1d }, Minimum: 1d, Increment: 1d, Name: "Радиусы полуосей")]
    public DVector2 Radius
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.FigureChange;
            }
        }
    }

    [DisplayNumericProperty(Default: 2, Increment: 0.1, Minimum: 1, Name: "Высота цилиндра")]
    public double CylHeight
    {
        get { return Get<double>(); }
        set
        {
            if (Set<double>(value))
            {
                _Commands |= Commands.FigureChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 50d, 3d }, Minimum: 3d, Increment: 1d, Name: "Апроксимация")]
    public DVector2 Approximation
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.NewFigure;
            }
        }
    }
    public int approx0, approx1;


    [DisplayNumericProperty(Default: new[] { 0d, 0d, 0d }, Increment: 0.1, Name: "Смещение")]
    public DVector3 Offset
    {
        get { return _Offset; }
        set
        {
            if (Set<DVector3>(value))
            {
                _Offset = value;
                UpdateTransformMatrix();
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 0d, 0d, 0d }, Increment: 1, Name: "Поворот")]
    public DVector3 Rotations
    {
        get { return _Rotations; }
        set
        {
            if (Set<DVector3>(value))
            {
                _Rotations = value;
                if (_Rotations.X >= 360) _Rotations.X -= 360;
                if (_Rotations.Y >= 360) _Rotations.Y -= 360;
                if (_Rotations.Z >= 360) _Rotations.Z -= 360;
                if (_Rotations.X < 0) _Rotations.X += 360;
                if (_Rotations.Y < 0) _Rotations.Y += 360;
                if (_Rotations.Z < 0) _Rotations.Z += 360;
                UpdateTransformMatrix();
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 1d, 1d, 1d }, Minimum: 0.1d, Increment: 0.1, Name: "Масштаб")]
    public DVector3 Scale
    {
        get { return _Scale; }
        set
        {
            _Scale = value;
            if (Set<DVector3>(value))
            {
                UpdateTransformMatrix();
            }
        }
    }

    public enum Projection
    {
        [Description("Не задана")] NotSet,
        [Description("Вид спереди")] InFront,
        [Description("Вид сверху")] Above,
        [Description("Вид сбоку")] Sideway,
        [Description("Изометрия")] Isometry
    }

    [DisplayEnumListProperty(Projection.NotSet, Name: "Проекция")]
    public Projection CurProjection
    {
        get { return Get<Projection>(); }
        set
        {
            if (Set<Projection>(value))
            {
                if (CurProjection == Projection.Above)
                {
                    _Scale.Y = 0;
                }
                else if (CurProjection == Projection.InFront)
                {
                    _Scale.Z = 0;
                }
                else if (CurProjection == Projection.Sideway)
                {
                    _Scale.X = 0;
                }
                else if (CurProjection == Projection.Isometry)
                {
                    _Rotations.X = 35;
                    _Rotations.Y = 45;
                    _Rotations.Z = 0;
                }
                UpdateTransformMatrix();
                _Commands |= Commands.Transform;
            }
        }
    }


    public enum Visualization
    {
        [Description("Каркасная визуализация")] NoPolygons,
        [Description("Одним цветом")] OneColor,
        [Description("Случайные цвета")] RandomColor,
    }
    [DisplayEnumListProperty(Visualization.OneColor, Name: "Способ отрисовки")]
    public abstract Visualization CurVisual { get; set; }

    [DisplayCheckerProperty(Default: false, Name: "Использовать буфер вершин")]
    public virtual bool useVBO
    {
        get { return Get<bool>(); }
        set { if (Set(value)) _Commands |= Commands.Transform; }
    }


    [DisplayNumericProperty(Default: new[] { 0.68d, 0.85d, 0.90d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Цвет материала")]
    public abstract DVector3 MaterialColor { get; set; }

    [DisplayNumericProperty(Default: new[] { 0d, 0d, 0d }, Minimum: 0d, Increment: 0.01d, Name: "Положение камеры")]
    public virtual DVector3 CameraPos {
        get { return Get<DVector3>(); }
        set { if (Set(value)) _Commands |= Commands.Transform; }
    }

    [DisplayNumericProperty(Default: -1.7d, Maximum: -0.1d, Increment: 0.1d, Decimals: 2, Name: "Удаленность камеры")]
    public virtual double CameraDistance
    {
        get { return Get<double>(); }
        set { if (Set(value)) _Commands |= Commands.Transform; }
    }

    [DisplayNumericProperty(Default: 60d, Maximum: 90d, Minimum: 30d, Increment: 1d, Decimals: 1, Name: "Поле зрения")]
    public virtual double FieldVision
    {
        get { return Get<double>(); }
        set { if (Set(value)) _Commands |= Commands.ChangeProjectionMatrix; }
    }

    [DisplayNumericProperty(Default: new[] { 0.1d, 100d }, Maximum: 1000d, Minimum: 0.1d, Increment: 0.1d, Decimals: 2, Name: "Плоскости отсечения")]
    public DVector2 ClippingPlanes
    {
        get { return _ClippingPlanes; }
        set { if (Set(value))
            {
                _ClippingPlanes = value;
                if (ClippingPlanes.X > ClippingPlanes.Y)
                {
                    _ClippingPlanes.X = _ClippingPlanes.Y;
                }
                _Commands |= Commands.ChangeProjectionMatrix;
            }
        }
    }
    public DVector2 _ClippingPlanes;

    public Vertex[] vertices;
    public uint[] indices;
    public uint[] vertexBuffer;
    public uint[] indexBuffer;


    public DMatrix4 pMatrix;

    #endregion

    #region Поля для работы с шейдерами
    private uint prog_shader;
    private uint vert_shader, frag_shader;
    private int attrib_coord, attrib_color, uniform_colour;
    #endregion

    #region Методы
    private void UpdateTransformMatrix()
    {
        _PointTransform = new DMatrix4(new double[] {1, 0, 0, 0,
                                                    0, 1, 0, 0,
                                                    0, 0, 1, 0,
                                                    0, 0, 0, 1 });
        /* Поворот */
        // По оси X
        DMatrix4 x_rotate = new DMatrix4(new double[] {1, 0, 0, 0,
                                                       0, Math.Cos(Math.PI / 180 * _Rotations.X), -Math.Sin(Math.PI / 180 * _Rotations.X), 0,
                                                       0, Math.Sin(Math.PI / 180 * _Rotations.X), Math.Cos(Math.PI / 180 * _Rotations.X), 0,
                                                       0, 0, 0, 1 });
        DMatrix4 y_rotate = new DMatrix4(new double[] {Math.Cos(Math.PI / 180 * _Rotations.Y), 0, Math.Sin(Math.PI / 180 * _Rotations.Y), 0,
                                                        0, 1, 0, 0,
                                                       -Math.Sin(Math.PI / 180 * _Rotations.Y), 0, Math.Cos(Math.PI / 180 * _Rotations.Y), 0,
                                                        0, 0, 0, 1 });
        DMatrix4 z_rotate = new DMatrix4(new double[] {Math.Cos(Math.PI / 180 * _Rotations.Z), -Math.Sin(Math.PI / 180 * _Rotations.Z), 0, 0,
                                                        Math.Sin(Math.PI / 180 * _Rotations.Z), Math.Cos(Math.PI / 180 * _Rotations.Z), 0, 0,
                                                        0, 0, 1, 0,
                                                        0, 0, 0, 1 });
        _PointTransform *= x_rotate;
        _PointTransform *= y_rotate;
        _PointTransform *= z_rotate;

        /* Маштабирование */
        _PointTransform *= new DMatrix4(new double[] {_Scale.X, 0, 0, 0,
                                                        0, _Scale.Y, 0, 0,
                                                        0, 0, _Scale.Z, 0 ,
                                                        0, 0, 0, 1 });



        /* Смещение */
        DMatrix4 offset = new DMatrix4(new double[] { 1, 0, 0, _Offset.X,
                                                      0, 1, 0, _Offset.Y,
                                                      0, 0, 1, _Offset.Z,
                                                      0, 0, 0, 1 });
        _PointTransform *= offset;

        _Commands |= Commands.Transform;

    }
    private void Create()
    {
        approx0 = (int) Approximation[0];
        approx1 = (int) Approximation[1];
        vertices = new Vertex[approx0 * approx1 + 2];
        indices = new uint[2 + approx0 + (approx1 - 1) * 2 * (approx0 + 1) + 2 + approx0];
    }
    private void Generate(DeviceArgs e)
    {
        double first_step = 2 * Math.PI / approx0;
        double second_step = CylHeight / (approx1 - 1);
        double height = 0;
        double middle = CylHeight / 2;

        var random = new Random();

        int indx = 0;

        vertices[indx] = new Vertex(0, 0, middle, 1, (byte) random.Next(256), (byte) random.Next(256), (byte) random.Next(256));
        ++indx;
        for (int k = 0; k < approx1; ++k)
        {
            double angle = 0;
            if (k + 1 == approx1) // Если дошли до нижней грани
            {
                vertices[indx] = new Vertex(0, 0, middle - height, 1, (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)); // Добавить центр эллипса
                ++indx;
            }
            for (int i = 0; i < approx0; ++i)
            {
                vertices[indx] = new Vertex(Radius[0] * Math.Cos(angle), Radius[1] * Math.Sin(angle), middle - height, 1,
                    (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                ++indx;
                angle += first_step;
            }
            height += second_step;
        }

        /* Полигоны верхнего основания */
        indx = 0;
        indices[indx] = 0;
        ++indx;
        for (int i = approx0; i > 0; --i)
        {
            indices[indx] = (uint) i;
            ++indx;
        }

        indices[indx] = (uint) approx0;
        ++indx;

        /* Боковые полигоны */
        for (int k = 1; k < approx1; ++k)
        {
            for (int i = 0; i < approx0; ++i)
            {
                indices[indx] = (uint) ((k + 1 == approx1 ? 2 : 1) + k * approx0 + i);
                ++indx;
                indices[indx] = (uint) (1 + (k - 1) * approx0 + i);
                ++indx;
            }
            indices[indx] = (uint)((k + 1 == approx1 ? 2 : 1) + k * approx0);
            ++indx;
            indices[indx] = (uint)(1 + (k - 1) * approx0);
            ++indx;
        }

        /* Полигоны нижнего основания */
        for (int i = 0; i <= approx0; ++i)
        {
            indices[indx] = (uint) ((approx1 - 1) * approx0 + 1 + i);
            ++indx;
        }
        indices[indx] = (uint) ( (approx1 - 1) * approx0 + 2 );
        ++indx;

        /* Загузка буферов */
        var gl = e.gl;
        vertexBuffer = new uint[1];
        indexBuffer = new uint[1];
        unsafe
        {
            /* Обработка массива вершин */
            gl.GenBuffers(1, vertexBuffer);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[0]);
            fixed (Vertex* ptr = &vertices[0])
            {
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices.Length * sizeof(Vertex), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);

            /* Обработка индексного массива */
            gl.GenBuffers(1, indexBuffer);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer[0]);
            fixed (uint* ptr = &indices[0])
            {
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.Length * sizeof(uint), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);

        }
    }

    private void UpdateProjectionMatrix(DeviceArgs e) // Задание матрицы проекций
    {
        var gl = e.gl;
        
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        pMatrix = Perspective(FieldVision, (double)e.Heigh / e.Width, ClippingPlanes.X, ClippingPlanes.Y);
        gl.LoadMatrix(pMatrix.ToArray(true));
    }    

    DMatrix4 Rotation(double x_rad, double y_rad, double z_rad)
    {
        DMatrix4 x_rotate = new DMatrix4(new double[] {1, 0, 0, 0,
                                                       0, Math.Cos(x_rad), -Math.Sin(x_rad), 0,
                                                       0, Math.Sin(x_rad), Math.Cos(x_rad), 0,
                                                       0, 0, 0, 1 });
        DMatrix4 y_rotate = new DMatrix4(new double[] {Math.Cos(y_rad), 0, Math.Sin(y_rad), 0,
                                                        0, 1, 0, 0,
                                                       -Math.Sin(y_rad), 0, Math.Cos(y_rad), 0,
                                                        0, 0, 0, 1 });
        DMatrix4 z_rotate = new DMatrix4(new double[] {Math.Cos(z_rad), -Math.Sin(z_rad), 0, 0,
                                                        Math.Sin(z_rad), Math.Cos(z_rad), 0, 0,
                                                        0, 0, 1, 0,
                                                        0, 0, 0, 1 });
        return x_rotate * y_rotate * z_rotate;
    }

    private void UpdateModelViewMatrix(DeviceArgs e)  // Задание объектно-видовой матрицы
    {
        var gl = e.gl;

        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        var deg2rad = Math.PI / 180; // Вращается камера, а не сам объект
        var cameraTransform = (DMatrix3)Rotation(deg2rad * CameraPos.X, deg2rad * CameraPos.Y, deg2rad * CameraPos.Z);
        var cameraPosition = cameraTransform * new DVector3(0, 0, CameraDistance);
        var cameraUpDirection = cameraTransform * new DVector3(0, 1, 0);

        // Мировая матрица (преобразование локальной системы координат в мировую)
        var mMatrix = _PointTransform;

        // Видовая матрица (переход из мировой системы координат к системе координат камеры)
        var vMatrix = LookAt(DMatrix4.Identity, cameraPosition, DVector3.Zero, cameraUpDirection);
        // матрица ModelView
        var mvMatrix = vMatrix * mMatrix;
        gl.LoadMatrix(mvMatrix.ToArray(true));

        _Commands |= Commands.Transform;
    }

    // Построение матрицы перспективной проекции
    private static DMatrix4 Perspective(double verticalAngle, double aspectRatio, double nearPlane, double farPlane)
    {
        var radians = (verticalAngle / 2) * Math.PI / 180;
        var sine = Math.Sin(radians);
        if (nearPlane == farPlane || aspectRatio == 0 || sine == 0)
            return DMatrix4.Zero;
        var cotan = Math.Cos(radians) / sine;
        var clip = farPlane - nearPlane;
        return new DMatrix4(
        cotan / aspectRatio, 0, 0, 0,
        0, cotan, 0, 0,
        0, 0, -(nearPlane + farPlane) / clip, -(2.0 * nearPlane * farPlane) / clip,
        0, 0, -1.0, 1.0
        );
    }

    // Метод умножения матрицы на видовую матрицу, полученную из точки наблюдения
    private static DMatrix4 LookAt(DMatrix4 matrix, DVector3 eye, DVector3 center, DVector3 up)
    {
        var forward = (center - eye).Normalized();
        if (forward.ApproxEqual(DVector3.Zero, 0.00001))
            return matrix;
        var side = (forward * up).Normalized();
        var upVector = side * forward;
        var result = matrix * new DMatrix4(
        +side.X, +side.Y, +side.Z, 0,
        +upVector.X, +upVector.Y, +upVector.Z, 0,
        -forward.X, -forward.Y, -forward.Z, 0,
        0, 0, 0, 1
        );
        result.M14 -= result.M11 * eye.X + result.M12 * eye.Y + result.M13 * eye.Z;
        result.M24 -= result.M21 * eye.X + result.M22 * eye.Y + result.M23 * eye.Z;
        result.M34 -= result.M31 * eye.X + result.M32 * eye.Y + result.M33 * eye.Z;
        result.M44 -= result.M41 * eye.X + result.M42 * eye.Y + result.M43 * eye.Z;
        return result;
    }

    #endregion


    protected override void OnMainWindowLoad(object sender, EventArgs args)
    {
        // TODO: Инициализация данных
        
        base.RenderDevice.MouseMoveWithRightBtnDown += (s, e)
        => Offset += new DVector3(0.001 * Math.Abs(_Scale.X) * e.MovDeltaX, 0.001 * Math.Abs(_Scale.Y) * e.MovDeltaY, 0);
        base.RenderDevice.MouseMoveWithLeftBtnDown += (s, e)
        => Rotations += new DVector3(0.1 * e.MovDeltaY, 0.1 * e.MovDeltaX, 0);
        base.RenderDevice.MouseWheel += (s, e) => Scale += new DVector3(0.001 * e.Delta, 0.001 * e.Delta, 0.001 * e.Delta);

        base.RenderDevice.Resize += (o, eventArgs) =>
        {
            _Commands |= Commands.ChangeProjectionMatrix;
        };

        base.RenderDevice.VSync = 1;

        RenderDevice.AddScheduleTask((gl, s) =>
        {
            gl.Enable(OpenGL.GL_CULL_FACE);
            gl.CullFace(OpenGL.GL_BACK);
            gl.FrontFace(OpenGL.GL_CW);
            gl.ClearColor(0, 0, 0, 0);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LEQUAL);
            gl.ClearDepth(1.0f);
            gl.ClearStencil(0);
        });
    }


    protected override void OnDeviceUpdate(object s, DeviceArgs e)
    {
        var gl = e.gl;

        // Очиста буфера экрана и буфера глубины (иначе рисоваться будет поверх старого )
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);


        // Задание проекционной и объектно-видовой матрицы
        if (0 != ((int)_Commands & (int)Commands.ChangeProjectionMatrix))
        {
            _Commands ^= Commands.ChangeProjectionMatrix;
            UpdateProjectionMatrix(e);
        }

        if (0 != ((int)_Commands & (int)Commands.NewFigure))
        {
            _Commands ^= Commands.NewFigure;
            Create();
            _Commands |= Commands.FigureChange;
        }

        if (0 != ((int)_Commands & (int)Commands.FigureChange))
        {
            _Commands ^= Commands.FigureChange;
            Generate(e);
            _Commands |= Commands.Transform;
        }

        if (0 != ((int)_Commands & (int)Commands.Transform))
        {
            _Commands ^= Commands.Transform;

            UpdateModelViewMatrix(e);

            // Задание способа визуализации
            if (CurVisual == Visualization.OneColor)
            {
                gl.PolygonMode(OpenGL.GL_FRONT, OpenGL.GL_FILL);
                gl.Color(MaterialColor.X, MaterialColor.Y, MaterialColor.Z);
            }
            else if (CurVisual == Visualization.RandomColor)
            {
                gl.PolygonMode(OpenGL.GL_FRONT, OpenGL.GL_FILL);
                gl.EnableClientState(OpenGL.GL_COLOR_ARRAY);
                unsafe
                {
                    if (!useVBO)
                    {
                        fixed (byte* ptr = &vertices[0].r)
                        {
                            gl.ColorPointer(3, OpenGL.GL_BYTE, sizeof(Vertex), (IntPtr)ptr);
                        }
                    }
                    else
                    {
                        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[0]);
                        gl.ColorPointer(3, OpenGL.GL_BYTE, sizeof(Vertex), (IntPtr) (sizeof(double) * 4));
                    }
                }
            }
            else if (CurVisual == Visualization.NoPolygons)
            {
                gl.PolygonMode(OpenGL.GL_FRONT, OpenGL.GL_LINE);
            }

            /* Непосредственная отрисовка */
            unsafe
            {
                gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
                if (!useVBO)
                {
                    fixed (double* ptr = &vertices[0].vx)
                    {
                        gl.VertexPointer(4, OpenGL.GL_DOUBLE, sizeof(Vertex), (IntPtr)ptr);
                    }
                }
                else
                {
                    gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[0]);
                    gl.VertexPointer(4, OpenGL.GL_DOUBLE, sizeof(Vertex), (IntPtr) 0);
                }

                if (useVBO)
                {
                    gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer[0]);
                    gl.DrawElements(OpenGL.GL_TRIANGLE_FAN, approx0 + 2, OpenGL.GL_UNSIGNED_INT, (IntPtr) 0);
                    for (int i = 1; i < approx1; ++i)
                    {
                        gl.DrawElements(OpenGL.GL_TRIANGLE_STRIP,  2 * (approx0 + 1), OpenGL.GL_UNSIGNED_INT, (IntPtr)((approx0 + 2 + (i-1) * 2 * (approx0 + 1)) * sizeof(uint)));
                    }
                   
                    gl.DrawElements(OpenGL.GL_TRIANGLE_FAN, approx0 + 2, OpenGL.GL_UNSIGNED_INT, (IntPtr) ((approx0 + 2 + (approx1 - 1) * 2 * (approx0 + 1)) * sizeof(uint)));
                }
                else
                {
                    fixed (uint* ptr = &indices[0])
                    {
                        gl.DrawElements(OpenGL.GL_TRIANGLE_FAN, approx0 + 2, ptr);
                        for (int i = 1; i < approx1; ++i)
                        {
                            gl.DrawElements(OpenGL.GL_TRIANGLE_STRIP, 2 * (approx0 + 1), ptr + approx0 + 2 + (i-1) * 2 * (approx0 + 1));
                        }
                        gl.DrawElements(OpenGL.GL_TRIANGLE_FAN, approx0 + 2, ptr + approx0 + 2 + (approx1 - 1) * 2 * (approx0 + 1));
                    }  
                }
                
            }

            if (useVBO)
            {
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
                gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
            }

            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            if (CurVisual == Visualization.RandomColor)
            {
                gl.DisableClientState(OpenGL.GL_COLOR_ARRAY);
            }
        }
    }
}
// ==================================================================================
public abstract class AppMain : CGApplication
{[STAThread] static void Main() { RunApplication(); } }