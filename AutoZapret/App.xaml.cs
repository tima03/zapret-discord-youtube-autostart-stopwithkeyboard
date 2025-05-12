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
                // Можно добавить логирование в файл:
                System.IO.File.AppendAllText("error.log", $"[UnhandledException] {DateTime.Now}: {ex}\n");
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Произошла ошибка:\n{args.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                System.IO.File.AppendAllText("error.log", $"[DispatcherUnhandledException] {DateTime.Now}: {args.Exception}\n");
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
