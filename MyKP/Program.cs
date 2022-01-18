using Device     = CGLabPlatform.OGLDevice;
using DeviceArgs = CGLabPlatform.OGLDeviceUpdateArgs;
using SharpGL;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        public Vertex(float px, float py, float pz, float pw, byte pr, byte pg, byte pb)
        {
            vx = px;
            vy = py;
            vz = pz;
            vw = pw;
            nx = 0;
            ny = 0;
            nz = 0;
            nw = 0;
            r = pr;
            g = pg;
            b = pb;
        }
        public readonly float vx, vy, vz, vw;
        public float nx, ny, nz, nw;
        public readonly byte r, g, b;
    }
    private void ChangeNormale(ref Vertex vertex, DVector4 normale)
    {
        DVector4 check = new DVector4(vertex.nx, vertex.ny, vertex.nz, 0);
        check += normale;
        vertex.nx = (float) check.X;
        vertex.ny = (float) check.Y;
        vertex.nz = (float) check.Z;
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
        ChangeProjectionMatrix = 1 << 4,
        ShadingChange = 1 << 5,
        ChangeLightPos = 1 << 6
    }
    private Commands _Commands = Commands.FigureChange;

    #region Работа с афинными преобразованиями фигуры

    #region Свойства
    private DMatrix4 _PointTransform = new DMatrix4(new[] { 1d, 0, 0, 0,
                                                            0, 1d, 0, 0,
                                                            0, 0, 1d, 0,
                                                            0, 0, 0, 1d });
    private DVector3 _Rotations;
    private DVector3 _Offset;
    private DVector3 _Scale;

    public enum Presets
    {
        [Description("Пресет 1")] Preset1,
        [Description("Пресет 2")] Preset2,
        [Description("Пресет 3")] Preset3,
        [Description("Пресет 4")] Preset4
    }
    [DisplayEnumListProperty(Presets.Preset1, Name: "Пресет фигуры")]
    public Presets CurPreset 
    {
        get { return Get<Presets>(); } 
        set
        {
            if (Set<Presets>(value))
            {
                _Commands |= Commands.NewFigure;
            }
        }
    }



    [DisplayNumericProperty(Default: 16, Minimum: 3d, Increment: 1d, Name: "Число граней")]
    public int NumSeg
    {
        get { return Get<int>(); }
        set
        {
            if (Set<int>(value))
            {
                _Commands |= Commands.NewFigure;
            }
        }
    }

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

    [DisplayNumericProperty(Default: new[] { 0.1d, 0.1d, 0.1d }, Minimum: 0.1d, Increment: 0.01, Name: "Масштаб")]
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

    #endregion

    #endregion

    #region Создание фигуры

    public static DMatrix4 Scaling(float x, float y, float z)
    {
        DMatrix4 r = DMatrix4.Identity;

        r[1, 1] = x;
        r[2, 2] = y;
        r[3, 3] = z;
        return r;
    }

    public static DMatrix4 RotateY(float ang)
    {
        return RotateAxis(ang, 1, 3);
    }

    public static DMatrix4 RotateAxis(float ang, int ax1, int ax2)
    {
        DMatrix4 r = DMatrix4.Identity;

        float sn = (float)Math.Sin(ang),
            cs = (float)Math.Cos(ang);

        r[ax1, ax1] = cs;
        r[ax1, ax2] = sn;
        r[ax2, ax1] = -sn;
        r[ax2, ax2] = cs;

        return r;
    }


    // Точки
    List<DVector3> Points = new List<DVector3>();
    List<DVector3> SplinePoints = new List<DVector3>();

    // cardinal spline
    public void calcSpline(ref List<DVector3> res, float tension, int numOfSeg)
    {
        res.Clear();
        if (Points.Count < 4) return;

        var cache = new float[(numOfSeg + 1) * 4];
        int i, cachePtr;

        cache[0] = 1;                               // 1,0,0,0
        cachePtr = 4;
        for (i = 1; i < numOfSeg; i++)
        {
            float st = i * 1.0f / numOfSeg,
                st2 = st * st,
                st3 = st2 * st,
                st23 = st3 * 2,
                st32 = st2 * 3;

            cache[cachePtr++] = st23 - st32 + 1;    // c1
            cache[cachePtr++] = st32 - st23;        // c2
            cache[cachePtr++] = st3 - 2 * st2 + st; // c3
            cache[cachePtr++] = st3 - st2;          // c4
        }

        cache[++cachePtr] = 1;                      // 0,1,0,0

        List<DVector3> pts = new List<DVector3>(Points);
        pts.Insert(0, Points[0]);
        pts.Add(Points[Points.Count - 1]);

        for (i = 1; i < Points.Count; i++)
        {
            DVector3
                pt1 = pts[i],
                pt3 = pts[i + 1];

            DVector3
                t1 = (pt3 - pts[i - 1]) * tension,
                t2 = (pts[i + 2] - pt1) * tension;

            for (int t = 0; t < numOfSeg; t++)
            {

                var c = t * 4;

                float
                    c1 = cache[c],
                    c2 = cache[c + 1],
                    c3 = cache[c + 2],
                    c4 = cache[c + 3];

                res.Add(c1 * pt1 + c2 * pt3 + c3 * t1 + c4 * t2);
            } // */                
        }
    }

    DVector4[,] mPoints;

    public void calcModel(int w)
    {
        int x, y, h;
        float xm, ym;

        DMatrix4 m1;

        h = SplinePoints.Count;

        mPoints = new DVector4[w + 1, h];

        xm = 1;
        ym = 1; // 0.5f; 
        float scale = 0.05f;


        // Расчет модели
        for (x = 0; x <= w; x++)
        {
            // Матрица очередного куска фигуры
            m1 = Scaling(scale * xm, scale, scale * ym) *
                RotateY((float)(x * 2.0 / w * Math.PI));

            for (y = 0; y < h; y++)
            {
                DVector4 p4 = new DVector4(SplinePoints[y], 1);
                mPoints[x, y] = m1 * p4;
            }
        }
        return;
    }


    int[,] quad =
    {
            { 0, 0 },
            { 1, 0 },
            { 1, 1 },

            { 0, 0 },
            { 1, 1 },
            { 0, 1 },
    };

    public void finishCalcModel(int w, DeviceArgs e)
    {
        // Окончательный расчет модели
        var random = new Random();
        int h, x, y, i;

        h = SplinePoints.Count;

        vertices = new Vertex[(w) * h];
        indices = new uint[w * (h - 1) * 6];

        for (x = 0; x < w; ++x)
        {
            for (y = 0; y < h; ++y)
            {
                var v = mPoints[x, y];
                vertices[x*h + y] = new Vertex((float)v[0], (float)v[1], (float)v[2], (float)v[3], (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }

        int ind_indx = 0;

        for (x = 0; x < w; x++)
        {
            for (y = 0; y < h - 1; y++)
            {
                var p1 = mPoints[x + 1, y] - mPoints[x, y];
                var p2 = mPoints[x, y + 1] - mPoints[x, y];
                var n = -(p1 * p2);

                for (i = 0; i < 6; i++)
                {
                    var indx = (x + quad[i, 0]) * h + (y + quad[i, 1]);
                    if (indx >= h * w) indx -= h * w;
                    ChangeNormale(ref vertices[indx], n);
                    indices[ind_indx++] = (uint) indx;
                }
            }
        }

        /* Нормализация нормалей вершин */
        for (int j = 0; j < vertices.Length; ++j)
        {
            DVector4 vec4 = new DVector4(vertices[j].nx, vertices[j].ny, vertices[j].nz, vertices[j].nw);
            vec4.Normalize();
            vertices[j].nx = (float)vec4.X;
            vertices[j].ny = (float)vec4.Y;
            vertices[j].nz = (float)vec4.Z;
            vertices[j].nw = (float)vec4.W;
        }

        normalPoints = new DVector4[((w) * h) * 2];
        normalIndices = new uint[((w) * h) * 2];

        /* Инициализация массивов для отрисовки нормалей */
        double normalLength = 0.25;
        for (i = 0; i < vertices.Length; ++i)
        {
            normalPoints[2 * i] = new DVector4(vertices[i].vx, vertices[i].vy, vertices[i].vz, vertices[i].vw);
            normalPoints[2 * i + 1] = new DVector4(vertices[i].vx + normalLength * vertices[i].nx,
                vertices[i].vy + normalLength * vertices[i].ny,
                vertices[i].vz + normalLength * vertices[i].nz,
                vertices[i].vw + normalLength * vertices[i].nw);
        }
        for (i = 0; i < normalIndices.Length; ++i)
        {
            normalIndices[i] = (uint)i;
        }

        /* Инициализация массивов для отрисовки опорных точек */

        headPoints = new DVector4[Points.Count];
        headIndicies = new uint[Points.Count];

        for (i = 0; i < headPoints.Length; ++i)
        {
            headPoints[i] = new DVector4(Points[i], 1);
            headPoints[i] = Scaling(0.05f, 0.05f, 0.05f) * headPoints[i];
            headIndicies[i] = (uint)i;
        }

        /* Загрузка буферов */
        var gl = e.gl;
        unsafe
        {
            /* Обработка массива вершин */
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[0]);
            fixed (Vertex* ptr = &vertices[0])
            {
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices.Length * sizeof(Vertex), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }

            /* Обработка индексного массива */
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer[0]);
            fixed (uint* ptr = &indices[0])
            {
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.Length * sizeof(uint), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }

            /* Обработка массива нормалей */
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, normalDataBuffer[0]);
            fixed (DVector4* ptr = &normalPoints[0])
            {
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, normalPoints.Length * sizeof(DVector4), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }

            /* Обработка индексного массива нормалей */
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, normalIndexBuffer[0]);
            fixed (uint* ptr = &normalIndices[0])
            {
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, normalIndices.Length * sizeof(uint), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }

            /* Обработка массива опорных точек */
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, headBuffer[0]);
            fixed (DVector4* ptr = &headPoints[0])
            {
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, headPoints.Length * sizeof(DVector4), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }

            /* Обработка индексного массива нормалей */
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, headIndexBuffer[0]);
            fixed (uint* ptr = &headIndicies[0])
            {
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, headIndicies.Length * sizeof(uint), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
            }
        }
    }


    void Generate(DeviceArgs e)
    {
        StreamReader file = new StreamReader("preset1.txt");
        if (CurPreset == Presets.Preset1)
        {
            file = new StreamReader("preset1.txt");
        }
        else if (CurPreset == Presets.Preset2)
        {
            file = new StreamReader("preset2.txt");
        }
        else if (CurPreset == Presets.Preset3)
        {
            file = new StreamReader("preset3.txt");
        }
        else if (CurPreset == Presets.Preset4)
        {
            file = new StreamReader("preset4.txt");
        }

        string[] words =  file.ReadToEnd().Split(' ', '\n');
        Points = new List<DVector3>();
        for (int i = 0; i < words.Length; i += 2)
        {
            Points.Add(new DVector3(Convert.ToInt32(words[i]), Convert.ToInt32(words[i + 1]), 0));
        }
        file.Close();

        int numSeg = NumSeg;

        calcSpline(ref SplinePoints, 0.5f, numSeg);
        calcModel(numSeg);
        finishCalcModel(numSeg, e);
    }

    #endregion

    public enum Visualization
    {
        [Description("Каркасная визуализация")] NoPolygons,
        [Description("Одним цветом")] OneColor,
        [Description("Случайные цвета")] RandomColor,
        [Description("Метод затенения Фонга")] PhongShading
    }
    [DisplayEnumListProperty(Visualization.OneColor, Name: "Способ отрисовки")]
    public abstract Visualization CurVisual { get; set; }

    #region Свойства освещения

    [DisplayCheckerProperty(Default: false, Name: "Показывать источник света")]
    public abstract bool isLightActive { get; set; }

    [DisplayCheckerProperty(Default: false, Name: "Показывать нормали вершин")]
    public abstract bool isNormalActive { get; set; }

    [DisplayCheckerProperty(Default: false, Name: "Показывать опорные точки")]
    public abstract bool isHeadlActive { get; set; }


    [DisplayNumericProperty(Default: new[] { 0.68d, 0.85d, 0.90d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Цвет материала")]
    public DVector3 MaterialColor {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 0.14d, 0.14d, 0.20d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Ka материала")]
    public DVector3 Ka_Material
    {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 1d, 1d, 0.54d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Kd материала")]
    public DVector3 Kd_Material
    {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 0.21d, 0.21d, 1d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Ks материала")]
    public DVector3 Ks_Material
    {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: 1d, Minimum: 0.01d, Maximum: 100d, Increment: 0.01d, Name: "p материала")]
    public double P_Material
    {
        get { return Get<double>(); }
        set
        {
            if (Set<double>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 1d, 1d, 1d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Ia освещения")]
    public DVector3 Ia_Material
    {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 1d, 0.5d, 0d }, Minimum: 0d, Maximum: 1d, Increment: 0.01d, Name: "Il освещения")]
    public DVector3 Il_Material
    {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    [DisplayNumericProperty(Default: new[] { 10d, 10d, 2d }, Increment: 0.01d, Name: "Pos освещения")]
    public DVector3 LightPos
    {
        get { return Get<DVector3>(); }
        set
        {
            if (Set<DVector3>(value))
            {
                _Commands |= Commands.ChangeLightPos;
            }
        }
    }
    public DVector4 LightPos_InWorldSpace;
    public uint[] LightVertexBuffer = new uint[1];
    public uint[] LightIndexBuffer = new uint[1];

    public uint[] LightIndexValues = new uint[1] { 0 };
    public double[] LightVertexArray;

    [DisplayNumericProperty(Default: new[] { 0.1d, 0.35d }, Minimum: 0.01d, Maximum: 100d, Increment: 0.01d, Name: "md, mk")]
    public DVector2 Parameters
    {
        get { return Get<DVector2>(); }
        set
        {
            if (Set<DVector2>(value))
            {
                _Commands |= Commands.ShadingChange;
            }
        }
    }

    #endregion

    #region Свойства отображения

    [DisplayNumericProperty(Default: new[] { 0d, 0d, 0d }, Minimum: 0d, Increment: 0.01d, Name: "Положение камеры")]
    public virtual DVector3 CameraPos {
        get { return Get<DVector3>(); }
        set { if (Set(value)) _Commands |= Commands.ShadingChange; }
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

    #endregion

    #region Поля для работы с шейдерами

    private uint prog_shader;
    private uint vert_shader, frag_shader;

    private int uniform_Ka_Material, uniform_Kd_Material, uniform_Ks_Material;
    private int uniform_P_Material;
    private int uniform_Ia_Material, uniform_Il_Material;
    private int uniform_LightPos, uniform_Parameters, uniform_CameraPos;
    private int uniform_FragColor;

    private int uniform_Projection, uniform_ModelView, uniform_NormalMatrix, uniform_PointMatrix;

    private int attribute_normale, attribute_coord;

    #endregion

    public DVector2 _ClippingPlanes;

    public Vertex[] vertices;
    public uint[] indices;
    public uint[] vertexBuffer = new uint[1];
    public uint[] indexBuffer = new uint[1];

    public DVector4[] normalPoints;
    public uint[] normalIndices;
    public uint[] normalDataBuffer = new uint[1];
    public uint[] normalIndexBuffer = new uint[1];

    public DVector4[] headPoints;
    public uint[] headIndicies;
    public uint[] headBuffer = new uint[1];
    public uint[] headIndexBuffer = new uint[1];


    public DMatrix4 pMatrix;


    #region Работа со светом
    public void UpdateLightValues(DeviceArgs e)
    {
        var gl = e.gl;
        gl.Uniform3(uniform_Ka_Material, (float)Ka_Material.X, (float) Ka_Material.Y, (float) Ka_Material.Z);
        gl.Uniform3(uniform_Kd_Material, (float)Kd_Material.X, (float)Kd_Material.Y, (float) Kd_Material.Z);
        gl.Uniform3(uniform_Ks_Material, (float)Ks_Material.X, (float)Ks_Material.Y, (float)Ks_Material.Z);

        gl.Uniform1(uniform_P_Material, (float)P_Material);

        gl.Uniform3(uniform_Ia_Material, (float)Ia_Material.X, (float)Ia_Material.Y, (float)Ia_Material.Z);
        gl.Uniform3(uniform_Il_Material, (float)Il_Material.X, (float)Il_Material.Y, (float)Il_Material.Z);

        DVector4 LightPosV4 = new DVector4(LightPos, 1);
        LightPos_InWorldSpace = _PointTransform * LightPosV4;
        gl.Uniform3(uniform_LightPos, (float)LightPos_InWorldSpace.X, (float)LightPos_InWorldSpace.Y, (float)LightPos_InWorldSpace.Z);
        gl.Uniform2(uniform_Parameters, (float)Parameters.X, (float)Parameters.Y);
        gl.Uniform3(uniform_CameraPos, (float)CameraPos.X, (float)CameraPos.Y, (float)CameraPos.Z);

        gl.Uniform3(uniform_FragColor, (float)MaterialColor.X, (float)MaterialColor.Y, (float)MaterialColor.Z);
    }
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

    private void UpdateProjectionMatrix(DeviceArgs e) // Задание матрицы проекций
    {
        var gl = e.gl;
        
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        pMatrix = Perspective(FieldVision, 1, ClippingPlanes.X, ClippingPlanes.Y);
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


    public DMatrix4 ModelViewMatrix;
    public DMatrix4 NormalMatrix;
    float[] ConvertToFloatArray(DMatrix4 Matrix)
    {
        float[] MatrixF = new float[16];
        double[] MatrixD = Matrix.ToArray();
        for (int i = 0; i < 16; ++i)
        {
            MatrixF[i] = (float)MatrixD[i];
        }
        return MatrixF;
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
        ModelViewMatrix = vMatrix * mMatrix;
        gl.LoadMatrix(ModelViewMatrix.ToArray(true));

        // Матрица преобразования вектора
        NormalMatrix = DMatrix3.NormalVecTransf(mMatrix);

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
            //gl.Enable(OpenGL.GL_CULL_FACE);
            //gl.CullFace(OpenGL.GL_BACK);
            gl.FrontFace(OpenGL.GL_CW);
            gl.ClearColor(0, 0, 0, 0);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LEQUAL);
            gl.ClearDepth(1.0f);
            gl.ClearStencil(0);

            gl.GenBuffers(1, vertexBuffer);
            gl.GenBuffers(1, indexBuffer);

            gl.GenBuffers(1, LightVertexBuffer);
            gl.GenBuffers(1, LightIndexBuffer);

            gl.GenBuffers(1, normalDataBuffer);
            gl.GenBuffers(1, normalIndexBuffer);

            gl.GenBuffers(1, headBuffer);
            gl.GenBuffers(1, headIndexBuffer);

        });


        #region Загрузка и комплиция шейдера  ------------------

        RenderDevice.AddScheduleTask((gl, s) => {

            var parameters = new int[1];

            prog_shader = gl.CreateProgram();
            if (prog_shader == 0)
                throw new Exception("OpenGL Error: не удалось создать шейдерную программу");

            var load_and_compile = new Func<uint, string, uint>(
                (shader_type, shader_name) =>
                {
                    var shader = gl.CreateShader(shader_type);
                    if (shader == 0)
                        throw new Exception("OpenGL Error: не удалось создать объект шейдера");
                    var source = HelpUtils.GetTextFileFromRes(shader_name);
                    gl.ShaderSource(shader, source);
                    gl.CompileShader(shader);

                    gl.GetShader(shader, OpenGL.GL_COMPILE_STATUS, parameters);
                    if (parameters[0] != OpenGL.GL_TRUE)
                    {
                        gl.GetShader(shader, OpenGL.GL_INFO_LOG_LENGTH, parameters);
                        var stringBuilder = new StringBuilder(parameters[0]);
                        gl.GetShaderInfoLog(shader, parameters[0], IntPtr.Zero, stringBuilder);
                        Debug.WriteLine("\n\n\n\n ====== SHADER GL_COMPILE_STATUS: ======");
                        Debug.WriteLine(stringBuilder);
                        Debug.WriteLine("==================================");
                        throw new Exception("OpenGL Error: ошибка во при компиляции " + (
                            shader_type == OpenGL.GL_VERTEX_SHADER ? "вершиного шейдера"
                            : shader_type == OpenGL.GL_FRAGMENT_SHADER ? "фрагментного шейдера"
                            : "какого-то еще шейдера"));
                    }

                    gl.AttachShader(prog_shader, shader);
                    return shader;
                });

            vert_shader = load_and_compile(OpenGL.GL_VERTEX_SHADER, "shader1.vert");
            frag_shader = load_and_compile(OpenGL.GL_FRAGMENT_SHADER, "shader2.frag");

            gl.LinkProgram(prog_shader);
            gl.GetProgram(prog_shader, OpenGL.GL_LINK_STATUS, parameters);
            if (parameters[0] != OpenGL.GL_TRUE)
            {
                gl.GetProgram(prog_shader, OpenGL.GL_INFO_LOG_LENGTH, parameters);
                var stringBuilder = new StringBuilder(parameters[0]);
                gl.GetProgramInfoLog(prog_shader, parameters[0], IntPtr.Zero, stringBuilder);
                Debug.WriteLine("\n\n\n\n ====== PROGRAM GL_LINK_STATUS: ======");
                Debug.WriteLine(stringBuilder);
                Debug.WriteLine("==================================");
                throw new Exception("OpenGL Error: ошибка линковкой");
            }
               
        });

        #endregion

        #region Удаление шейдера

        RenderDevice.Closed += (s, e) => RenderDevice.AddScheduleTask((gl, _s) => {
            gl.UseProgram(0);
            gl.DeleteProgram(prog_shader);
            gl.DeleteShader(vert_shader);
            gl.DeleteShader(frag_shader);
        });

        #endregion


        #region Связывание аттрибутов и юниформ ------------------

        RenderDevice.AddScheduleTask((gl, s) => {

            /* Использующиеся в shader2.frag */

            uniform_Ka_Material = gl.GetUniformLocation(prog_shader, "Ka_Material");
            if (uniform_Ka_Material < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Ka_Material");

            uniform_Kd_Material = gl.GetUniformLocation(prog_shader, "Kd_Material");
            if (uniform_Kd_Material < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Kd_Material");

            uniform_Ks_Material = gl.GetUniformLocation(prog_shader, "Ks_Material");
            if (uniform_Ks_Material < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Ks_Material");

            uniform_P_Material = gl.GetUniformLocation(prog_shader, "P_Material");
            if (uniform_P_Material < 0 )
                throw new Exception("OpenGL Error: не удалость связать аттрибут P_Material");

            uniform_Ia_Material = gl.GetUniformLocation(prog_shader, "Ia_Material");
            if (uniform_Ia_Material < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Ia_Material");

            uniform_Il_Material = gl.GetUniformLocation(prog_shader, "Il_Material");
            if (uniform_Il_Material < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Il_Material");

            uniform_LightPos = gl.GetUniformLocation(prog_shader, "LightPos");
            if (uniform_LightPos < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут LightPos");

            uniform_Parameters = gl.GetUniformLocation(prog_shader, "Parameters");
            if (uniform_Parameters < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Parameters");

            uniform_CameraPos = gl.GetUniformLocation(prog_shader, "CameraPos");
            if (uniform_CameraPos < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут CameraPos");

            /* Использующиеся в shader1.vert */
            attribute_normale = gl.GetAttribLocation(prog_shader, "Normal");
            if (attribute_normale < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Normal");

            attribute_coord = gl.GetAttribLocation(prog_shader, "Coord");
            if (attribute_coord < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Coord");

            uniform_Projection = gl.GetUniformLocation(prog_shader, "Projection");
            if (uniform_Projection < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут Projection");

            uniform_ModelView = gl.GetUniformLocation(prog_shader, "ModelView");
            if (uniform_ModelView < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут ModelView");

            uniform_FragColor = gl.GetUniformLocation(prog_shader, "FragColor");
            if (uniform_FragColor < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут FragColor");

            uniform_NormalMatrix = gl.GetUniformLocation(prog_shader, "NormalMatrix");
            if (uniform_NormalMatrix < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут NormalMatrix");

            uniform_PointMatrix = gl.GetUniformLocation(prog_shader, "PointMatrix");
            if (uniform_PointMatrix < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут PointMatrix");

        });

        #endregion

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
            _Commands |= Commands.Transform;
        }

        if (0 != ((int)_Commands & (int)Commands.NewFigure))
        {
            _Commands ^= Commands.NewFigure;
            Generate(e);
            _Commands |= Commands.FigureChange;
        }

        if (0 != ((int)_Commands & (int)Commands.FigureChange))
        {
            _Commands ^= Commands.FigureChange;
            Generate(e);
        }

        if (0 != ((int)_Commands & (int)Commands.Transform))
        {
            _Commands ^= Commands.Transform;
            UpdateModelViewMatrix(e);
        }

        if (0 != ((int)_Commands & (int) Commands.ChangeLightPos)) // Здесь загружаются данные света
        {
            _Commands ^= Commands.ChangeLightPos;

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, LightVertexBuffer[0]);
            DVector4 LightPosV4 = new DVector4(LightPos, 1);
            unsafe
            {
                LightVertexArray = LightPosV4.ToArray();
                
                fixed (double* ptr = &LightVertexArray[0])
                {
                    gl.BufferData(OpenGL.GL_ARRAY_BUFFER, LightVertexArray.Length * sizeof(double), (IntPtr) ptr, OpenGL.GL_STATIC_DRAW);
                }
            }

            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, LightIndexBuffer[0]);
            unsafe
            {
                fixed (uint* ptr = &LightIndexValues[0])
                {
                    gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, LightIndexValues.Length * sizeof(uint), (IntPtr)ptr, OpenGL.GL_STATIC_DRAW);
                }
            }

            LightPos_InWorldSpace = _PointTransform * LightPosV4;

        }

        // Задание способа визуализации
        if (CurVisual == Visualization.OneColor)
        {
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
            gl.Color(MaterialColor.X, MaterialColor.Y, MaterialColor.Z);
        }
        else if (CurVisual == Visualization.RandomColor)
        {
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
            gl.EnableClientState(OpenGL.GL_COLOR_ARRAY);
            unsafe
            {
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[0]);
                gl.ColorPointer(3, OpenGL.GL_BYTE, sizeof(Vertex), (IntPtr)(sizeof(float) * 8));
            }
        }
        else if (CurVisual == Visualization.NoPolygons)
        {
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
            gl.Color(MaterialColor.X, MaterialColor.Y, MaterialColor.Z);
        }
        else if (CurVisual == Visualization.PhongShading)
        {
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
        }

        /* Непосредственная отрисовка */
        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vertexBuffer[0]);
        gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer[0]);

        if (CurVisual == Visualization.PhongShading)
        {
            gl.UseProgram(prog_shader);

            UpdateLightValues(e);
            gl.UniformMatrix4(uniform_ModelView, 1, true, ConvertToFloatArray(ModelViewMatrix));
            gl.UniformMatrix4(uniform_Projection, 1, true, ConvertToFloatArray(pMatrix));
            gl.UniformMatrix4(uniform_NormalMatrix, 1, true, ConvertToFloatArray(NormalMatrix));
            gl.UniformMatrix4(uniform_PointMatrix, 1, true, ConvertToFloatArray(_PointTransform));

            gl.EnableVertexAttribArray((uint)attribute_normale);
            gl.EnableVertexAttribArray((uint)attribute_coord);
            unsafe
            {
                gl.VertexAttribPointer((uint)attribute_normale, 4, OpenGL.GL_FLOAT, false, sizeof(Vertex), (IntPtr)(4 * sizeof(float)));
                gl.VertexAttribPointer((uint)attribute_coord, 4, OpenGL.GL_FLOAT, false, sizeof(Vertex), (IntPtr)0);
            }
        }
        else
        {
            gl.UseProgram(0);
            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            unsafe
            {
                gl.VertexPointer(4, OpenGL.GL_FLOAT, sizeof(Vertex), (IntPtr)0);
            }
        }

        gl.DrawElements(OpenGL.GL_TRIANGLES, indices.Length, OpenGL.GL_UNSIGNED_INT, (IntPtr)0);

        if (CurVisual == Visualization.PhongShading)
        {
            gl.DisableVertexAttribArray((uint)attribute_normale);
            gl.DisableVertexAttribArray((uint)attribute_coord);
            gl.UseProgram(0);
        }
        else
        {
            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
        }

        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
        gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);

        if (CurVisual == Visualization.RandomColor)
        {
            gl.DisableClientState(OpenGL.GL_COLOR_ARRAY);
        }

        if (isLightActive)
        {
            gl.Color(0.99, 0, 0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, LightVertexBuffer[0]);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, LightIndexBuffer[0]);
            
            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            unsafe
            {
                gl.VertexPointer(4, OpenGL.GL_DOUBLE, sizeof(DVector4), (IntPtr)0);
            }
            
            gl.PointSize(10);
            gl.DrawElements(OpenGL.GL_POINTS, 1, OpenGL.GL_UNSIGNED_INT, (IntPtr)0);
            
            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
        }

        if (isNormalActive)
        {
            gl.Color(0, 0, 0.99);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, normalDataBuffer[0]);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, normalIndexBuffer[0]);

            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            unsafe
            {
                gl.VertexPointer(4, OpenGL.GL_DOUBLE, sizeof(DVector4), (IntPtr)0);
            }

            gl.DrawElements(OpenGL.GL_LINES, normalPoints.Length, OpenGL.GL_UNSIGNED_INT, (IntPtr)0);

            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
        }

        if (isHeadlActive)
        {
            gl.Color(0, 0.99, 0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, headBuffer[0]);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, headIndexBuffer[0]);

            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            unsafe
            {
                gl.VertexPointer(4, OpenGL.GL_DOUBLE, sizeof(DVector4), (IntPtr)0);
            }

            gl.PointSize(10);
            gl.DrawElements(OpenGL.GL_POINTS, headIndicies.Length, OpenGL.GL_UNSIGNED_INT, (IntPtr)0);

            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
        }

    }
}
// ==================================================================================
public abstract class AppMain : CGApplication
{[STAThread] static void Main() { RunApplication(); } }