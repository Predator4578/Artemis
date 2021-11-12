﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Artemis.UI.Avalonia.Shared.Exceptions;
using Artemis.UI.Avalonia.Shared.Services.Builders;
using Artemis.UI.Avalonia.Shared.Services.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FluentAvalonia.UI.Controls;
using Ninject;
using Ninject.Parameters;

namespace Artemis.UI.Avalonia.Shared.Services
{
    internal class WindowService : IWindowService
    {
        private readonly IKernel _kernel;
        private bool _exceptionDialogOpen;

        public WindowService(IKernel kernel)
        {
            _kernel = kernel;
        }

        public T ShowWindow<T>(params (string name, object value)[] parameters)
        {
            IParameter[] paramsArray = parameters.Select(kv => new ConstructorArgument(kv.name, kv.value)).Cast<IParameter>().ToArray();
            T viewModel = _kernel.Get<T>(paramsArray)!;
            ShowWindow(viewModel);
            return viewModel;
        }

        public void ShowWindow(object viewModel)
        {
            Window parent = GetCurrentWindow();

            string name = viewModel.GetType().FullName!.Split('`')[0].Replace("ViewModel", "View");
            Type? type = viewModel.GetType().Assembly.GetType(name);

            if (type == null)
            {
                throw new ArtemisSharedUIException($"Failed to find a window named {name}.");
            }

            if (!type.IsAssignableTo(typeof(Window)))
            {
                throw new ArtemisSharedUIException($"Type {name} is not a window.");
            }

            Window window = (Window) Activator.CreateInstance(type)!;
            window.DataContext = viewModel;
            window.Show(parent);
        }

        public async Task<TResult> ShowDialogAsync<TViewModel, TResult>(params (string name, object value)[] parameters) where TViewModel : DialogViewModelBase<TResult>
        {
            IParameter[] paramsArray = parameters.Select(kv => new ConstructorArgument(kv.name, kv.value)).Cast<IParameter>().ToArray();
            TViewModel viewModel = _kernel.Get<TViewModel>(paramsArray)!;
            return await ShowDialogAsync(viewModel);
        }

        public async Task<bool> ShowConfirmContentDialog(string title, string message, string confirm = "Confirm", string? cancel = "Cancel")
        {
            ContentDialogResult contentDialogResult = await CreateContentDialog()
                .WithTitle(title)
                .WithContent(message)
                .HavingPrimaryButton(b => b.WithText(confirm))
                .WithCloseButtonText(cancel)
                .ShowAsync();

            return contentDialogResult == ContentDialogResult.Primary;
        }

        public async Task<TResult> ShowDialogAsync<TResult>(DialogViewModelBase<TResult> viewModel)
        {
            Window parent = GetCurrentWindow();

            string name = viewModel.GetType().FullName!.Split('`')[0].Replace("ViewModel", "View");
            Type? type = viewModel.GetType().Assembly.GetType(name);

            if (type == null)
            {
                throw new ArtemisSharedUIException($"Failed to find a window named {name}.");
            }

            if (!type.IsAssignableTo(typeof(Window)))
            {
                throw new ArtemisSharedUIException($"Type {name} is not a window.");
            }

            Window window = (Window) Activator.CreateInstance(type)!;
            window.DataContext = viewModel;
            return await window.ShowDialog<TResult>(parent);
        }

        public void ShowExceptionDialog(string title, Exception exception)
        {
            if (_exceptionDialogOpen)
            {
                return;
            }

            try
            {
                _exceptionDialogOpen = true;
                ShowDialogAsync(new ExceptionDialogViewModel(title, exception)).GetAwaiter().GetResult();
            }
            finally
            {
                _exceptionDialogOpen = false;
            }
        }

        public ContentDialogBuilder CreateContentDialog()
        {
            return new ContentDialogBuilder(_kernel, GetCurrentWindow());
        }

        public OpenFileDialogBuilder CreateOpenFileDialog()
        {
            return new OpenFileDialogBuilder(GetCurrentWindow());
        }

        public SaveFileDialogBuilder CreateSaveFileDialog()
        {
            return new SaveFileDialogBuilder(GetCurrentWindow());
        }

        public Window GetCurrentWindow()
        {
            if (Application.Current.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime classic)
            {
                throw new ArtemisSharedUIException("Can't show a dialog when application lifetime is not IClassicDesktopStyleApplicationLifetime.");
            }

            Window parent = classic.Windows.FirstOrDefault(w => w.IsActive) ?? classic.MainWindow;
            return parent;
        }
    }
}