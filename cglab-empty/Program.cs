#define UseOpenGL // Раскомментировать для использования OpenGL
#if (!UseOpenGL)
using Device = CGLabPlatform.GDIDevice;
using DeviceArgs = CGLabPlatform.GDIDeviceUpdateArgs;
#else
using Device = CGLabPlatform.OGLDevice;
using DeviceArgs = CGLabPlatform.OGLDeviceUpdateArgs;
using SharpGL;
#endif

using System;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Collections.Generic;
using CGLabPlatform;
// ==================================================================================


using CGApplication = MyOGLApp;

public abstract class MyOGLApp : CGApplicationTemplate<CGApplication, Device, DeviceArgs>
{
    protected override void OnMainWindowLoad(object sender, EventArgs args)
    {
        base.RenderDevice.VSync = 1;
        base.MainWindow.StartPosition = FormStartPosition.CenterScreen;
        base.MainWindow.Size = new Size(1280, 720);
        base.RenderDevice.Resized += (s, e) => { };
        
        #region Загрузка и комплиция шейдера  ------------------
    
        RenderDevice.AddScheduleTask((gl, s) => {
            
            var parameters = new int[1];
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
                    if (parameters[0] != OpenGL.GL_TRUE) {
                        gl.GetShader(shader, OpenGL.GL_INFO_LOG_LENGTH, parameters);
                        var stringBuilder = new StringBuilder(parameters[0]);
                        gl.GetShaderInfoLog(shader, parameters[0], IntPtr.Zero, stringBuilder);
                        Debug.WriteLine("\n\n\n\n ====== SHADER GL_COMPILE_STATUS: ======");
                        Debug.WriteLine(stringBuilder);
                        Debug.WriteLine("==================================");
                        throw new Exception("OpenGL Error: ошибка во при компиляции " + (
                            shader_type == OpenGL.GL_VERTEX_SHADER ? "вершиного шейдера"
                            : shader_type == OpenGL.GL_FRAGMENT_SHADER ? "фрагментного шейдера"
                            : "какого-то еще щеёдера"));
                    }
                    
                    gl.AttachShader(prog_shader, shader);
                    return shader;
                });

            prog_shader = gl.CreateProgram();
            if (prog_shader == 0)
                throw new Exception("OpenGL Error: не удалось создать шейдерную программу");

            vert_shader = load_and_compile(OpenGL.GL_VERTEX_SHADER,   "sample.vert");
            frag_shader = load_and_compile(OpenGL.GL_FRAGMENT_SHADER, "sample.frag");
            
            gl.LinkProgram(prog_shader);
            gl.GetProgram(prog_shader, OpenGL.GL_LINK_STATUS, parameters);
            if (parameters[0] != OpenGL.GL_TRUE)
                throw new Exception("OpenGL Error: ошибка линковкой");
        });
    
        #endregion
        
        #region Удаление шейдера
        
        RenderDevice.Closed += (s,e ) => RenderDevice.AddScheduleTask((gl, _s) => {
            gl.UseProgram(0);
            gl.DeleteProgram(prog_shader);
            gl.DeleteShader(vert_shader);
            gl.DeleteShader(frag_shader);
        });

        #endregion
        
        #region Связывание аттрибутов и юниформ ------------------
    
        RenderDevice.AddScheduleTask((gl, s) => {
            attrib_coord = gl.GetAttribLocation(prog_shader, "coord");
            if (attrib_coord < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут coord");
            
            attrib_color = gl.GetAttribLocation(prog_shader, "color");
            if (attrib_color < 0)
                throw new Exception("OpenGL Error: не удалость связать аттрибут color");

            uniform_time = gl.GetUniformLocation(prog_shader, "time");
            if (uniform_time < 0)
                throw new Exception("OpenGL Error: не удалость связать юниформы time");
        });
    
        #endregion
        
    }
    
    #region Свойства

    [DisplayCheckerProperty(true, "Использовать шейдер")]
    public virtual bool useShader { get; set; }
    private uint prog_shader;
    private uint vert_shader, frag_shader;
    private int attrib_coord, attrib_color, uniform_time;
    private float cur_time = 0;
    
    #endregion

    #region Полигон

    private struct Vertex  {
        public readonly float x, y;
        public readonly float r, g, b;
        public Vertex(float x, float y, float r, float g, float b) {
            this.x = x; this.y = y; this.r = r; this.g = g; this.b = b;
        }
    }
    
    //    0           1           2   
    //  | Vertex1   | Vertex2   | Vertex3   |
    //  | 0 4 8 
    //  | x,y,r,g,b | x,y,r,g,b | x,y,r,g,b |    ID
    //  |     R G B       R G B
    // 
    private static readonly Vertex[] vertices = {
        //            vx    vy     r      g      b
        new Vertex(-1f, -1f,  1f, .5f,  0f),
        new Vertex(+1f, -1f,  0f,  1f, .5f),
        new Vertex(+1f, +1f,  1f,  0f, .5f),
        new Vertex(-1f, +1f, .5f, .5f,  1f)
    };

    #endregion


    protected override void OnDeviceUpdate(object s, DeviceArgs e) {
        var gl = e.gl;
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);
        
        gl.UseProgram(useShader ? prog_shader : 0);
        gl.Uniform1(uniform_time, cur_time += e.Delta / 1000f);
        
        
        gl.Begin(OpenGL.GL_QUADS);
        for (int i = vertices.Length; 0 != i--;) {
            var v = vertices[i];
            if (useShader) {
                gl.VertexAttrib2((uint)attrib_coord, v.x, v.y);
                gl.VertexAttrib3((uint)attrib_color, v.r, v.g, v.b);
            } else {
                gl.Vertex(v.x, v.y);
                gl.Color(v.r, v.g, v.b);
            }
        }
        gl.End();
    }
}


// ==================================================================================
public abstract class AppMain : CGApplication
{[STAThread] static void Main() { RunApplication(); } }