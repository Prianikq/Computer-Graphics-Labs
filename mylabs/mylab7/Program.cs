using Device     = CGLabPlatform.OGLDevice;
using DeviceArgs = CGLabPlatform.OGLDeviceUpdateArgs;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Linq;
using SharpGL;
using CGLabPlatform;
// ==================================================================================


using CGApplication = MyApp;
using System.ComponentModel;

public abstract class MyApp : OGLApplicationTemplate<MyApp>
{
    #region Классы
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Point
    {
        public Point(double px, double py)
        {
            x = px;
            y = py;
            z = 0;
            w = 1;
        }
        public Point(DVector2 p)
        {
            x = p.X;
            y = p.Y;
            z = 0;
            w = 1;
        }
        public double x, y, z, w;
    }
    #endregion

    [DisplayNumericProperty(Default: 100, Increment: 1, Minimum: 10, Maximum: 10000, Name: "Апроксимация")]
    public int PointsCount
    {
        get { return Get<int>(); }
        set
        {
            if (Set<int>(value))
            {
                _Commands |= Commands.Create;
            }
        }
    }


    [DisplayNumericProperty(Default: new[] { 0d, 1d }, Increment: 0.1, Name: "Точка 1")]
    public DVector2 point1
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ChangeFigure;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 10d, 1d }, Increment: 0.1, Name: "Точка 2")]
    public DVector2 point2
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ChangeFigure;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 20d, 1d }, Increment: 0.1, Name: "Точка 3")]
    public DVector2 point3
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ChangeFigure;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 30d, 1d }, Increment: 0.1, Name: "Точка 4")]
    public DVector2 point4
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ChangeFigure;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 40d, 1d }, Increment: 0.1, Name: "Точка 5")]
    public DVector2 point5
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ChangeFigure;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 50d, 1d }, Increment: 0.1, Name: "Точка 6")]
    public DVector2 point6
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ChangeFigure;
            }
        }
    }


    public uint[] vertexBuffer = new uint[2];
    public uint[] indexBuffer = new uint[2];

    public uint[] indices;
    public Point[] points;
    public Point[] pointsInWindow;

    public Point[] generators = new Point[6];
    public Point[] generatorsInWindow = new Point[6];
    public uint[] gen_indices = new uint[6] { 0, 1, 2, 3, 4, 5 };

    [Flags]
    private enum Commands : int
    {
        None = 0,
        Create = 1 << 0,
        ChangeFigure = 1 << 1,
        ChangeWindow = 1 << 2
    }

    private Commands _Commands = Commands.Create;

    public double y_min = double.MaxValue, y_max = double.MinValue;
    public double x_min = double.MaxValue, x_max = double.MinValue;
    public double Width, Heigh;
    public DVector2 ViewSize;
    public DVector2 Automove;
    public DVector2 AutoScale;

    public double WindowScale = 1;
    public DVector2 WindowMove = new DVector2(0d, 0d);


    protected override void OnMainWindowLoad(object sender, EventArgs args)
    {
        // TODO: Инициализация данных
        base.RenderDevice.MouseMoveWithLeftBtnDown += (s, e)
        =>
        { WindowMove += new DVector2(0.1 * e.MovDeltaY, 0.1 * e.MovDeltaX); _Commands |= Commands.ChangeWindow; };


        base.RenderDevice.MouseWheel += (s, e) => { WindowScale += 0.001 * e.Delta; _Commands |= Commands.ChangeWindow; };

        base.RenderDevice.Resize += (s, e) => 
        {
            _Commands |= Commands.ChangeWindow;
        };

        base.RenderDevice.VSync = 1;


        RenderDevice.AddScheduleTask((gl, s) =>
        {
            //gl.Enable(OpenGL.GL_CULL_FACE);
            //gl.CullFace(OpenGL.GL_BACK);
            //gl.FrontFace(OpenGL.GL_CW);
            gl.ClearColor(0, 0, 0, 0);

            //gl.Enable(OpenGL.GL_DEPTH_TEST);
            //gl.DepthFunc(OpenGL.GL_LEQUAL);
            //gl.ClearDepth(1.0f);
            gl.ClearStencil(0);
        });
    }

    double LagrangePolynomial(double x, Point[] args)
    {
        double Result = 0;
        for (int i = 0; i < args.Length; ++i)
        {
            double product_res = 1;
            for (int j = 0; j < args.Length; ++j)
            {
                if (i == j) continue;

                product_res *= (x - args[j].x) / (args[i].x - args[j].x);

            }
            Result += args[i].y * product_res;
        }
        return Result;
    }



    protected Point FromViewToPhysicalSpace(Point point)
    {
        point.x = point.x * AutoScale.X + Automove.X; // Преобразование координат из видового пространства в физическое
        point.y = point.y * AutoScale.Y + Automove.Y;
        point.x *= WindowScale; // Изменение размера в окне отрисовки
        point.y *= WindowScale;
        point.x += WindowMove.X; // Изменение положения в окне отрисовки
        point.y += WindowMove.Y;
        return point;
    }


    protected void WorkWithPoints(DeviceArgs e, Point[] points, Point[] pointsInWindow, uint[] indices, uint ver_id, uint ind_id)
    {
        var gl = e.gl;

        for (int i = 0; i < pointsInWindow.Length; ++i)
        {
            pointsInWindow[i] = new Point();
            pointsInWindow[i] = FromViewToPhysicalSpace(points[i]); // Перевод в с.к. окна
            /* Перевод в с.к. устройства вывода с использованием OpenGL */
            pointsInWindow[i].x /= Width;
            pointsInWindow[i].y /= Heigh;
            pointsInWindow[i].x -= 0.5d;
            pointsInWindow[i].y -= 0.5d;
            pointsInWindow[i].y *= -1;
            pointsInWindow[i].x *= 2;
            pointsInWindow[i].y *= 2;
            Console.WriteLine(i + " " + pointsInWindow[i].x + " " + pointsInWindow[i].y);
        }

        unsafe
        {
            /* Обработка массива вершин */

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[ver_id]);

            fixed (Point* ptr = &pointsInWindow[0])
            {
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, pointsInWindow.Length * sizeof(Point), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);

            /* Обработка индексного массива */

            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer[ind_id]);

            fixed (uint* ptr = &indices[0])
            {
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.Length * sizeof(uint), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
        }
    }

    protected void DrawGraphic(DeviceArgs e, uint ver_id, uint ind_id, uint sz, uint draw_type, double r, double g, double b)
    {
        var gl = e.gl;

        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[ver_id]);
        gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
        unsafe
        {
            gl.VertexPointer(4, OpenGL.GL_DOUBLE, sizeof(Point), (IntPtr)0);
        }

        gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer[ind_id]);

        gl.Color(r, g, b);
        gl.PointSize(sz);
        gl.DrawElements(draw_type, indices.Length, OpenGL.GL_UNSIGNED_INT, (IntPtr)0);

        gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
        gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
    }



    protected override void OnDeviceUpdate(object s, DeviceArgs e)
    {
        var gl = e.gl;

        // Очиста буфера экрана и буфера глубины (иначе рисоваться будет поверх старого )
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);

        if (0 != ((int)_Commands & (int)Commands.Create))
        {
            _Commands ^= Commands.Create;
            points = new Point[PointsCount];
            pointsInWindow = new Point[PointsCount];
            indices = new uint[PointsCount];
            _Commands |= Commands.ChangeFigure;
        }

        if (0 != ((int)_Commands & (int)Commands.ChangeFigure))
        {
            _Commands ^= Commands.ChangeFigure;

            generators = new Point[] {new Point(point1), new Point(point2), new Point(point3),
                                                                                new Point(point4), new Point(point5), new Point(point6)};
            x_min = double.MaxValue;
            x_max = double.MinValue;
            y_min = double.MaxValue;
            y_max = double.MinValue;

            for (int i = 0; i < generators.Length; ++i)
            {
                x_min = Math.Min(x_min, generators[i].x);
                x_max = Math.Max(x_max, generators[i].x);
            }

            double x_cur = x_min;
            double step = (x_max - x_min) / (PointsCount - 1);

            for (int i = 0; i < points.Length; ++i)
            {

                points[i] = new Point(x_cur, LagrangePolynomial(x_cur, generators));
                indices[i] = (uint)i;

                x_cur += step;

                y_min = Math.Min(y_min, points[i].y);
                y_max = Math.Max(y_max, points[i].y);
            }

            _Commands |= Commands.ChangeWindow;
        }

        if (0 != ((int)_Commands & (int)Commands.ChangeWindow))
        {
            _Commands ^= Commands.ChangeWindow;

            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();

            Width = e.Width;
            Heigh = e.Heigh;
            ViewSize.X = x_max - x_min;
            ViewSize.Y = y_max - y_min;
            AutoScale.X = .9 * Width / ViewSize.X;
            AutoScale.Y = .9 * Heigh / ViewSize.Y;
            AutoScale.X = AutoScale.Y = Math.Min(AutoScale.X, AutoScale.Y);
            Automove.X = Width / 2 - (x_min + x_max) / 2 * AutoScale.X;
            Automove.Y = Heigh / 2 - (y_min + y_max) / 2 * AutoScale.Y;

            gl.GenBuffers(2, vertexBuffer);
            gl.GenBuffers(2, indexBuffer);

            WorkWithPoints(e, points, pointsInWindow, indices, 0, 0);
            WorkWithPoints(e, generators, generatorsInWindow, gen_indices, 1, 1);
        }

        DrawGraphic(e, 0, 0, 2, OpenGL.GL_LINE_STRIP, 0.99, 0, 0);
        DrawGraphic(e, 1, 1, 4, OpenGL.GL_POINTS, 0.99, 0.64, 0);

    }
}
// ==================================================================================
public abstract class AppMain : CGApplication
{[STAThread] static void Main() { RunApplication(); } }