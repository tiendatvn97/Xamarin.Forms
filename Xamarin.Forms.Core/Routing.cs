﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xamarin.Forms
{
	public static class Routing
	{
		static int s_routeCount = 0;
		static Dictionary<string, RouteFactory> s_routes = new Dictionary<string, RouteFactory>();
		static Dictionary<string, Page> s_implicitPageRoutes = new Dictionary<string, Page>();

		const string ImplicitPrefix = "IMPL_";
		const string DefaultPrefix = "D_FAULT_";
		internal const string PathSeparator = "/";

		internal static void RegisterImplicitPageRoute(Page page)
		{
			s_implicitPageRoutes[GetRoute(page)] = page;
		}

		List<Page> BuildFlattenedNavigationStack(List<Page> startingList, IReadOnlyList<Page> modalStack)
		{
			if (modalStack == null)
				return startingList;

			for (int i = 0; i < modalStack.Count; i++)
			{
				startingList.Add(modalStack[i]);
				for (int j = 1; j < modalStack[i].Navigation.NavigationStack.Count; j++)
				{
					startingList.Add(modalStack[i].Navigation.NavigationStack[j]);
				}
			}

			return startingList;
		}

		internal static string GenerateImplicitRoute(string source)
		{
			if (IsImplicit(source))
				return source;

			return String.Concat(ImplicitPrefix, source);
		}

		internal static bool IsImplicit(string source)
		{
			return source.StartsWith(ImplicitPrefix, StringComparison.Ordinal);
		}

		internal static bool IsImplicit(BindableObject source)
		{
			return IsImplicit(GetRoute(source));
		}

		internal static bool IsDefault(string source)
		{
			return source.StartsWith(DefaultPrefix, StringComparison.Ordinal);
		}

		internal static bool IsDefault(BindableObject source)
		{
			return IsDefault(GetRoute(source));
		}

		internal static bool IsUserDefined(BindableObject source)
		{
			if (source == null)
				return false;

			return !(IsDefault(source) || IsImplicit(source));
		}

		internal static void Clear()
		{
			s_routes.Clear();
		}

		public static readonly BindableProperty RouteProperty =
			BindableProperty.CreateAttached("Route", typeof(string), typeof(Routing), null,
				defaultValueCreator: CreateDefaultRoute);

		static object CreateDefaultRoute(BindableObject bindable)
		{
			return $"{DefaultPrefix}{bindable.GetType().Name}{++s_routeCount}";
		}

		internal static string[] GetRouteKeys()
		{
			string[] keys = new string[s_routes.Count + s_implicitPageRoutes.Count];
			s_routes.Keys.CopyTo(keys, 0);
			return keys;
		}

		public static Element GetOrCreateContent(string route)
		{
			Element result = null;

			if (s_routes.TryGetValue(route, out var content))
				result = content.GetOrCreate();

			if (result == null)
			{
				// okay maybe its a type, we'll try that just to be nice to the user
				var type = Type.GetType(route);
				if (type != null)
					result = Activator.CreateInstance(type) as Element;
			}

			if (result != null)
				SetRoute(result, route);

			return result;
		}

		public static string GetRoute(BindableObject obj)
		{
			return (string)obj.GetValue(RouteProperty);
		}

		internal static string GetRoutePathIfNotImplicit(Element obj)
		{
			var source = GetRoute(obj);
			if (IsImplicit(source))
				return String.Empty;

			return $"{source}/";
		}

		public static string FormatRoute(List<string> segments)
		{
			var route = FormatRoute(String.Join(PathSeparator, segments));
			return route;
		}

		public static string FormatRoute(string route)
		{
			return route;
		}

		public static void RegisterRoute(string route, RouteFactory factory)
		{
			if (!String.IsNullOrWhiteSpace(route))
				route = FormatRoute(route);
			ValidateRoute(route, factory);

			s_routes[route] = factory;
		}

		public static void UnRegisterRoute(string route)
		{
			if (s_routes.TryGetValue(route, out _))
				s_routes.Remove(route);
		}

		public static void RegisterRoute(string route, Type type)
		{
			RegisterRoute(route, new TypeRouteFactory(type));
		}

		public static void SetRoute(Element obj, string value)
		{
			obj.SetValue(RouteProperty, value);
		}

		static void ValidateRoute(string route, RouteFactory routeFactory)
		{
			if (string.IsNullOrWhiteSpace(route))
				throw new ArgumentNullException(nameof(route), "Route cannot be an empty string");

			routeFactory = routeFactory ?? throw new ArgumentNullException(nameof(routeFactory), "Route Factory cannot be null");

			var uri = new Uri(route, UriKind.RelativeOrAbsolute);

			var parts = uri.OriginalString.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var part in parts)
			{
				if (IsImplicit(part))
					throw new ArgumentException($"Route contains invalid characters in \"{part}\"");
			}

			RouteFactory existingRegistration = null;
			if(s_routes.TryGetValue(route, out existingRegistration) && !existingRegistration.Equals(routeFactory))
				throw new ArgumentException($"Duplicated Route: \"{route}\"");
		}

		class TypeRouteFactory : RouteFactory
		{
			readonly Type _type;

			public TypeRouteFactory(Type type)
			{
				_type = type;
			}

			public override Element GetOrCreate()
			{
				return (Element)Activator.CreateInstance(_type);
			}
			public override bool Equals(object obj)
			{
				if ((obj is TypeRouteFactory typeRouteFactory))
					return typeRouteFactory._type == _type;

				return false;
			}

			public override int GetHashCode()
			{
				return _type.GetHashCode();
			}
		}
	}
}