﻿//#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using FreeMote.Plugins;
using FreeMote.Psb;
using FreeMote.PsBuild;
using McMaster.Extensions.CommandLineUtils;

namespace FreeMote.Tools.Viewer
{
    public static class Core
    {
        public static uint Width { get; set; } = 1280;
        public static uint Height { get; set; } = 720;
        public static uint Top { get; set; } = 0;
        public static uint Left { get; set; } = 0;
        public static float Scale { get; set; } = 1.0f;
        public static bool DirectLoad { get; set; } = false;
        public static List<string> OriPaths{get;set;}
        public static List<string> PsbPaths { get; set; }=new List<string>();
        internal static bool NeedRemoveTempFile { get; set; } = false;
    }

    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Console.WriteLine("FreeMote Viewer");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            Console.WriteLine();
            Logger.InitConsole();
            var app = new CommandLineApplication();
            app.OptionsComparison = StringComparison.OrdinalIgnoreCase;

            //help
            //var optHelp = app.HelpOption("-?|--help"); //do not inherit
            app.ExtendedHelpText = PrintHelp();

            //options
            var optHelp = app.Option("-?|--help", "Show help", CommandOptionType.NoValue);
            var optWidth = app.Option<uint>("-w|--width", "Set Window width", CommandOptionType.SingleValue);
            var optHeight = app.Option<uint>("-h|--height", "Set Window height", CommandOptionType.SingleValue);
            var optDirectLoad = app.Option("-d|--direct", "Just load with EMT driver, don't try parsing with FreeMote first", CommandOptionType.NoValue);
            var optFixMetadata = app.Option("-nf|--no-fix", "Don't try to apply metadata fix (for partial exported or krkr PSBs). Can't work together with `-d`", CommandOptionType.NoValue);
            var optConfigFile = app.Option<string>("-c|--config", "Using config file show psb", CommandOptionType.SingleValue);

            //args
            var argPath = app.Argument("Files", "File paths", multipleValues: true);

            app.OnExecute(() =>
            {
                if ((argPath.Values.Count == 0 || optHelp.HasValue())&&!optConfigFile.HasValue())
                {
                    var help = app.GetHelpText();
                    MessageBox.Show(help, "FreeMote Viewer Help", MessageBoxButton.OK, MessageBoxImage.Information);
                    app.ShowHelp();
                    return;
                }

                if (optConfigFile.HasValue())
                {
                    var filename = optConfigFile.Value();
                    if (!File.Exists(filename))
                    {
                        Console.WriteLine($"Can't find config file:{filename}");
                        return;
                    }
                    var configJson = ConfigFile.Load(filename);
                    Core.OriPaths = configJson.FileNames;
                    Core.Width=configJson.Width;
                    Core.Height=configJson.Height;
                    Core.Top = configJson.Top;
                    Core.Left = configJson.Left;
                    Core.Scale = configJson.Scale;
                }
                else
                {
                    Core.OriPaths = argPath.Values.ToList();
                    if (optWidth.HasValue())
                    {
                        Core.Width = optWidth.ParsedValue;
                    }

                    if (optHeight.HasValue())
                    {
                        Core.Height = optHeight.ParsedValue;
                    }
                }

                
                Core.OriPaths.RemoveAll(f => !File.Exists(f));

                if (Core.OriPaths.Count == 0)
                {
                    Console.WriteLine("No file specified.");
                    return;
                }

                if (!optDirectLoad.HasValue())
                {
                    try
                    {
                        //Consts.FastMode = false;
                        FreeMount.Init();
                        var ctx = FreeMount.CreateContext();
                        for (int i = 0; i < Core.OriPaths.Count; i++)
                        {
                            using var fs = File.OpenRead(Core.OriPaths[i]);
                            string currentType = null;
                            using var ms = ctx.OpenFromShell(fs, ref currentType);
                            var psb = ms != null ? new PSB(ms) : new PSB(fs);

                            if (psb.Platform == PsbSpec.krkr) //common should be loadable
                            {
                                psb.SwitchSpec(PsbSpec.win, PsbSpec.win.DefaultPixelFormat());
                            }

                            if (!optFixMetadata.HasValue())
                            {
                                try
                                {
                                    psb.FixMotionMetadata();
                                    //psb.FixTimelineContentValueType();
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                            }

                            psb.Merge();
                            //File.WriteAllText("output.json", PsbDecompiler.Decompile(psb));
                            var tempFile = Path.GetTempFileName();
                            File.WriteAllBytes(tempFile, psb.Build());
                            Core.PsbPaths.Add(tempFile);
                            Core.NeedRemoveTempFile = true;
                        }

                        GC.Collect(); //Can save memory from 700MB to 400MB
                    }
                    catch (PsbBadFormatException ex)
                    {
                        MessageBox.Show("Can not load PSB, maybe your PSB is encrypted. \r\nUse EmtConvert to decrypt it first.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        CleanTempFiles();
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        CleanTempFiles();
                        return;
                    }
                }
                else
                {
                    Core.PsbPaths = Core.OriPaths;
                    Core.DirectLoad = true;
                }

                if (ConsoleExtension.HasMyConsole())
                {
                    ConsoleExtension.Hide();
                }
                App wpf = new App();
                MainWindow main = new MainWindow();
                wpf.Run(main);
            });

            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException)
            {
                Console.WriteLine("Could not parse the arguments.");
                app.ShowHelp();
                var help = app.GetHelpText();
                MessageBox.Show(help, "FreeMote Viewer Help", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return 0;
        }

        private static void CleanTempFiles()
        {
            if (Core.NeedRemoveTempFile && Core.PsbPaths?.Count > 0)
            {
                foreach (var tempPath in Core.PsbPaths)
                {
                    File.Delete(tempPath);
                }

                Core.NeedRemoveTempFile = false;
            }

            Core.PsbPaths = new List<string>();
        }

        private static string PrintHelp()
        {
            return @"Examples: 
  FreeMoteViewer sample.psb
  FreeMoteViewer -w 1920 -h 1080 -d sample.psb
  FreeMoteViewer -nf sample_head.psb sample_body.psb
Hint:
  You can load multiple partial exported PSB like the `-nf` example. 
  Use correct order: always try to put the Main part at last (body is the Main part comparing to head)!
  If you're picking multiple files from file explorer and drag&drop to Viewer, drag the non-Main part.";
        }
    }

    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
        }
    }
}

static class ConsoleExtension
{
    const int SW_HIDE = 0;
    const int SW_SHOW = 5;
    readonly static IntPtr handle = GetConsoleWindow();
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool FreeConsole();

    public static void Hide()
    {
        ShowWindow(handle, SW_HIDE); //hide the console
    }
    public static void Show()
    {
        ShowWindow(handle, SW_SHOW); //show the console
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcessId();

    [DllImport("user32.dll")]
    static extern int GetWindowThreadProcessId(IntPtr hWnd, ref IntPtr ProcessId);

    public static bool HasMyConsole()
    {
        IntPtr hConsole = GetConsoleWindow();
        IntPtr hProcessId = IntPtr.Zero;
        GetWindowThreadProcessId(hConsole, ref hProcessId);

        return GetCurrentProcessId().Equals(hProcessId);
    }
}