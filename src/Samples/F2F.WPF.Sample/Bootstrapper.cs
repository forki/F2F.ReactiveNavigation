﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using F2F.ReactiveNavigation;
using F2F.ReactiveNavigation.WPF;
using F2F.WPF.Sample.View;
using F2F.WPF.Sample.ViewModel;
using Microsoft.Practices.Unity;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive;
using F2F.WPF.Sample.Controller;
using Autofac;
using Autofac.Core;
using System.Reflection;
using Autofac.Features.OwnedInstances;
using Autofac.Features.Metadata;
using Autofac.Features.Indexed;
using F2F.ReactiveNavigation.ViewModel;

namespace F2F.WPF.Sample
{
	internal class Bootstrapper
	{
		private class AutofacViewModelFactory : ICreateViewModel
		{

			private readonly IIndex<Type, Func<Owned<ReactiveViewModel>>> _factories;

			public AutofacViewModelFactory(IIndex<Type, Func<Owned<ReactiveViewModel>>> factories)	
			{
				_factories = factories;
			}

			public ScopedLifetime<TViewModel> CreateViewModel<TViewModel>() where TViewModel : ReactiveViewModel
			{
				var factory = _factories[typeof(TViewModel)];

				var ownedViewModel = factory();
				
				return ((TViewModel)ownedViewModel.Value).Lifetime().EndingWith(ownedViewModel);
			}
		}

		private static int _viewModelCount = 0;

		public void Run()
		{
			var builder = new ContainerBuilder();

			builder
				.RegisterType<ViewFactory>()
				.AsImplementedInterfaces()
				.SingleInstance();

			builder
				.RegisterType<AutofacViewModelFactory>()
				.AsImplementedInterfaces();

			builder
				.RegisterType<DummyDisposable>()
				.AsSelf();

			builder
				.RegisterType<SampleController>()
				.AsImplementedInterfaces();

			builder
				.Register(c => Router.Create(c.Resolve<ICreateViewModel>(), RxApp.MainThreadScheduler))
				.SingleInstance();

			builder
				.RegisterAssemblyTypes(typeof(Bootstrapper).Assembly)
				.Where(t => typeof(IInitializer).IsAssignableFrom(t))
				.As<IInitializer>();

			builder
				.RegisterAssemblyTypes(typeof(Bootstrapper).Assembly)
				.Where(t => typeof(ReactiveViewModel).IsAssignableFrom(t))
				.Keyed<ReactiveViewModel>(t => t);

			var container = builder.Build();

			var shellBuilder = new ContainerBuilder();

			var shell = InitializeShell(container, shellBuilder);
			shellBuilder.Update(container);

			// maybe use an autofac type?!
			var initializers =  container.Resolve<IEnumerable<IInitializer>>();
			initializers.ToList().ForEach(i => i.Initialize());

			Application.Current.MainWindow = shell;
			shell.Show();
		}

		private Window InitializeShell(IContainer container, ContainerBuilder shellBuilder)
		{
			var shell = new MainWindow();
			
			var menuBuilder = new MenuBuilder(shell.MenuRegion);
			var router = container.Resolve<IRouter>();
			var tabRegion = router.AddRegion(Regions.TabRegion);
			
			menuBuilder.AddMenuItem("Add", () => AddNewView(router, tabRegion));

			shellBuilder.RegisterInstance<IMenuBuilder>(menuBuilder);

			var viewFactory = container.Resolve<ICreateView>();
			var tabRegionAdapter = new TabRegionAdapter(viewFactory, tabRegion, shell.TabRegion);
			tabRegionAdapter.Adapt();
			shellBuilder.RegisterInstance(tabRegionAdapter);

			return shell;
		}

		private static Task AddNewView(IRouter router, IRegion tabRegion)
		{
			var naviParams = NavigationParameters.Create();
			naviParams.Set("value", _viewModelCount++);
			return router.RequestNavigate<SampleViewModel>(tabRegion.Name, naviParams);
		}
	}
}