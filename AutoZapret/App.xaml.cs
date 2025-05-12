using System;
using System.Windows;

namespace AutoZapert
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Глобальная обработка непойманных исключений
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Произошла критическая ошибка:\n{ex?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                try
                {
                    (Application.Current.MainWindow as MainWindow)?.StopWinws(); // Добавь модификатор public к StopWinws()
                }
                catch { }

                System.IO.File.AppendAllText("error.log", $"[UnhandledException] {DateTime.Now}: {ex}\n");
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Произошла ошибка:\n{args.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

                try
                {
                    // Попытка остановить winws при ошибке в UI-потоке
                    (Application.Current.MainWindow as MainWindow)?.StopWinws();
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("error.log", $"[StopWinws Error] {DateTime.Now}: {ex}\n");
                }

                System.IO.File.AppendAllText("error.log", $"[DispatcherUnhandledException] {DateTime.Now}: {args.Exception}\n");

                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
