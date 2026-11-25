using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NetFix.Services;

using Application = System.Windows.Application;

namespace NetFix;

public partial class App : Application
{
    private const string MutexName = "BypassDPI-WPF_SingleInstance_Mutex";
    private const string PipeName = "BypassDPI-WPF_SingleInstance_Pipe";
    private const string ElevatedArgument = "--elevated";
    private static Mutex? _mutex;
    private static CancellationTokenSource? _pipeCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (Array.IndexOf(e.Args, ElevatedArgument) < 0 && !IsAdministrator() && TryElevate(e.Args))
        {
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        if (!SettingsService.IsLegalNoticeAccepted)
        {
            const string notice =
                "ВНИМАНИЕ! Вы запускаете неофициальный форк программы NetFix. " +
                "Оригинальный автор (rupleide) не имеет отношения к этой сборке, " +
                "не гарантирует её безопасность и не оказывает поддержку. " +
                "Оригинальный чистый NetFix доступен по адресу: github.com/rupleide/NetFix";

            System.Windows.MessageBox.Show(notice, "Неофициальная сборка",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            SettingsService.MarkLegalNoticeAccepted();
        }

        base.OnStartup(e);
        StartPipeServer();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client);
            writer.WriteLine("ACTIVATE");
            writer.Flush();
        }
        catch
        {
        }
    }

    private static void StartPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_pipeCts.Token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_pipeCts.Token);
                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync(_pipeCts.Token);
                    if (line == "ACTIVATE")
                    {
                        Current?.Dispatcher.BeginInvoke(() =>
                        {
                            if (Current.MainWindow is MainWindow main)
                            {
                                main.ShowFromTray();
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(100);
                }
            }
        });
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool TryElevate(string[] args)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            startInfo.ArgumentList.Add(ElevatedArgument);
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            System.Windows.MessageBox.Show("Запуск от имени администратора отменён.", "BypassDPI-WPF",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Не удалось повысить права: {ex.Message}", "BypassDPI-WPF",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeCts?.Cancel();
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }
}
