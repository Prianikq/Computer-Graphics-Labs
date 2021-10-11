//#define UseOpenGL // Раскомментировать для использования OpenGL
#if (!UseOpenGL)
using Device     = CGLabPlatform.GDIDevice;
using DeviceArgs = CGLabPlatform.GDIDeviceUpdateArgs;
#else
using Device     = CGLabPlatform.OGLDevice;
using DeviceArgs = CGLabPlatform.OGLDeviceUpdateArgs;
using SharpGL;
#endif

using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Collections.Generic;
using CGLabPlatform;
// ==================================================================================


using CGApplication = MyApp;
public abstract class MyApp: CGApplicationTemplate<CGApplication, Device, DeviceArgs>
{
    // TODO: Добавить свойства, поля
    #region Свойства

    [DisplayNumericProperty(Default: 10, Increment: 1, Minimum: 0, Maximum: 1000, Name: "Константа функции а")]
    public abstract double A { get; set; }

    [DisplayNumericProperty(Default: 4, Increment: 1, Minimum: 1, Maximum: 10000, Name: "Число вершин")]
    public abstract int VertexCount { get; set; }

    [DisplayNumericProperty(Default: 0, Increment: 0.01, Minimum: 0, Maximum: 2 * Math.PI, Name: "Угол поворота")]
    public abstract double Angle { get; set; }

    [DisplayNumericProperty(Default: 1, Increment: 0.01, Minimum: 0.01, Maximum: 2, Name: "Размер в окне")]
    public abstract double WindowScale { get; set; }

    [DisplayNumericProperty(Default: new[] { 0d, 0d }, Increment: 0.1, Name: "Cмещение в окне")]
    public abstract DVector2 WindowMove{ get; set; }


    public DVector2 ViewSize;
    public DVector2 Automove;
    public DVector2 AutoScale;
    #endregion

    #region Методы
    protected DVector2 CoordinateTransformation(DVector2 point)
    {
        DVector2 result = new DVector2();
        var X = point.X;
        var Y = point.Y;
        result.X = X * Math.Cos(Angle) - Y * Math.Sin(Angle); // Операция поворота
        result.Y = X * Math.Sin(Angle) + Y * Math.Cos(Angle);
        return result;
    }
    protected DVector2 FromViewToPhysicalSpace(DVector2 point) 
    {
        point.X = point.X * AutoScale.X + Automove.X; // Преобразование координат из видового пространства в физическое
        point.Y = point.Y * AutoScale.Y + Automove.Y;
        point.X *= WindowScale; // Изменение размера в окне отрисовки
        point.Y *= WindowScale;
        point.X += WindowMove.X; // Изменение положения в окне отрисовки
        point.Y += WindowMove.Y;
        return point;
    }
    #endregion


    protected override void OnMainWindowLoad(object sender, EventArgs args)
    {
        // TODO: Инициализация данных
        base.RenderDevice.BufferBackCol = 0x20;
    }


    protected override void OnDeviceUpdate(object s, DeviceArgs e)
    {
        // TODO: Отрисовка и обновление
        double step = 2 * Math.PI / VertexCount;
        double angle = 0;
        double X, Y;
        List<DVector2> points = new List<DVector2>();
        while (angle < 2 * Math.PI)
        {
            X = A * (1 - Math.Cos(angle)) * Math.Cos(angle);
            Y = A * (1 - Math.Cos(angle)) * Math.Sin(angle);
            points.Add(new DVector2(X, Y));
            angle += step;
        }
        X = A * (1 - Math.Cos(0)) * Math.Cos(0);
        Y = A * (1 - Math.Cos(0)) * Math.Sin(0);
        points.Add(new DVector2(X, Y));

        for (int i = 0; i < points.Count; ++i)
        {
            points[i] = CoordinateTransformation(points[i]);
        }
        var x_min = points.Min(p => p.X);
        var x_max = points.Max(p => p.X);
        var y_min = points.Min(p => p.Y);
        var y_max = points.Max(p => p.Y);
        ViewSize.X = points.Max(p => p.X) - points.Min(p => p.X);
        ViewSize.Y = points.Max(p => p.Y) - points.Min(p => p.Y);
        AutoScale.X = .9 * e.Width / ViewSize.X;
        AutoScale.Y = .9 * e.Heigh / ViewSize.Y;
        AutoScale.X = AutoScale.Y = Math.Min(AutoScale.X, AutoScale.Y);
        Automove.X = e.Width / 2 - (x_min + x_max) / 2 * AutoScale.X;
        Automove.Y = e.Heigh / 2 - (y_min + y_max) / 2 * AutoScale.Y;

        for (int i = 1; i < points.Count; ++i)
        {
            e.Surface.DrawLine(Color.LawnGreen.ToArgb(), FromViewToPhysicalSpace(points[i]), FromViewToPhysicalSpace(points[i - 1]));
        }
    }

}
// ==================================================================================
public abstract class AppMain : CGApplication
{ [STAThread] static void Main() { RunApplication(); } }