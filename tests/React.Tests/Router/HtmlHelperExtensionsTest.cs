/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */
#if NET452

using System.Collections.Specialized;
using System.Web;
using System.Web.Mvc;
using Moq;
using React.Router;
using React.Tests.Core;
using Xunit;

namespace React.Tests.Router
{
	public class HtmlHelperExtensionsTest
	{
		/// <summary>
		/// Creates a mock <see cref="IReactEnvironment"/> and registers it with the IoC container
		/// This is only required because <see cref="HtmlHelperExtensions"/> can not be
		/// injected :(
		/// </summary>
		private ReactEnvironmentTest.Mocks ConfigureMockReactEnvironment()
		{
			var mocks = new ReactEnvironmentTest.Mocks();

			mocks.Engine.Setup(x => x.Evaluate<bool>("typeof ComponentName !== 'undefined'")).Returns(true);

			var environment = mocks.CreateReactEnvironment();
			AssemblyRegistration.Container.Register<IReactEnvironment>(environment);
			return mocks;
		}

		private Mock<IReactEnvironment> ConfigureMockEnvironment()
		{
			var environment = new Mock<IReactEnvironment>();
			AssemblyRegistration.Container.Register(environment.Object);
			return environment;
		}

		private Mock<IReactSiteConfiguration> ConfigureMockConfiguration()
		{
			var config = new Mock<IReactSiteConfiguration>();
			config.SetupGet(x => x.UseServerSideRendering).Returns(true);
			AssemblyRegistration.Container.Register(config.Object);
			return config;
		}

		private Mock<IReactIdGenerator> ConfigureReactIdGenerator()
		{
			var reactIdGenerator = new Mock<IReactIdGenerator>();
			AssemblyRegistration.Container.Register(reactIdGenerator.Object);
			return reactIdGenerator;
		}

		/// <summary>
		/// Mock an html helper with a mocked response object.
		/// Used when testing for server response modification.
		/// </summary>
		class HtmlHelperMocks
		{
			public Mock<HtmlHelper> htmlHelper;
			public Mock<HttpResponseBase> httpResponse;

			public HtmlHelperMocks(HttpRequestBase request = null)
			{
				var viewDataContainer = new Mock<IViewDataContainer>();
				var viewContext = new Mock<ViewContext>();
				httpResponse = new Mock<HttpResponseBase>();
				htmlHelper = new Mock<HtmlHelper>(viewContext.Object, viewDataContainer.Object);
				var httpContextBase = new Mock<HttpContextBase>();
				viewContext.Setup(x => x.HttpContext).Returns(httpContextBase.Object);
				httpContextBase.Setup(x => x.Request).Returns(request);
				httpContextBase.Setup(x => x.Response).Returns(httpResponse.Object);
			}
		}

		/// <summary>
		/// Mocks alot of common functionality related to rendering a
		/// React Router component.
		/// </summary>
		class ReactRouterMocks
		{
			public Mock<IReactSiteConfiguration> config;
			public Mock<IReactEnvironment> environment;
			public Mock<ReactRouterComponent> component;
			public Mock<IReactIdGenerator> reactIdGenerator;

			public ReactRouterMocks(
				Mock<IReactSiteConfiguration> conf,
				Mock<IReactEnvironment> env,
				Mock<IReactIdGenerator> idGenerator
			)
			{
				config = conf;
				environment = env;
				reactIdGenerator = idGenerator;

				component = new Mock<ReactRouterComponent>(
					environment.Object,
					config.Object,
					reactIdGenerator.Object,
					"ComponentName",
					"",
					"/"
				);
				var execResult = new Mock<ExecutionResult>();

				component.Setup(x => x.RenderRouterWithContext(It.IsAny<bool>(), It.IsAny<bool>(), null))
					.Returns(execResult.Object);
				environment.Setup(x => x.CreateComponent(
					It.IsAny<IReactComponent>(),
					It.IsAny<bool>()
				)).Returns(component.Object);
				environment.Setup(x => x.Execute<string>("JSON.stringify(context);"))
							.Returns("{ }");
			}
		}

		[Fact]
		public void EngineIsReturnedToPoolAfterRender()
		{
			var config = ConfigureMockConfiguration();
			var environment = ConfigureMockEnvironment();
			var reactIdGenerator = ConfigureReactIdGenerator();
			var routerMocks = new ReactRouterMocks(config, environment, reactIdGenerator);
			var htmlHelperMock = new HtmlHelperMocks();

			environment.Verify(x => x.ReturnEngineToPool(), Times.Never);
			var result = HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/",
				htmlTag: "span",
				clientOnly: true,
				serverOnly: true
			);
			environment.Verify(x => x.ReturnEngineToPool(), Times.Once);
		}

		[Fact]
		public void ReactWithClientOnlyTrueShouldCallRenderHtmlWithTrue()
		{
			var config = ConfigureMockConfiguration();

			var htmlHelperMock = new HtmlHelperMocks();
			var environment = ConfigureMockEnvironment();
			var reactIdGenerator = ConfigureReactIdGenerator();
			var routerMocks = new ReactRouterMocks(config, environment, reactIdGenerator);

			var result = HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/",
				htmlTag: "span",
				clientOnly: true,
				serverOnly: false
			);
			routerMocks.component.Verify(x => x.RenderRouterWithContext(It.Is<bool>(y => y == true), It.Is<bool>(z => z == false), null), Times.Once);
		}

		[Fact]
		public void ReactWithServerOnlyTrueShouldCallRenderHtmlWithTrue()
		{
			var config = ConfigureMockConfiguration();

			var htmlHelperMock = new HtmlHelperMocks();
			var environment = ConfigureMockEnvironment();
			var reactIdGenerator = ConfigureReactIdGenerator();
			var routerMocks = new ReactRouterMocks(config, environment, reactIdGenerator);

			var result = HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/",
				htmlTag: "span",
				clientOnly: false,
				serverOnly: true
			);
			routerMocks.component.Verify(x => x.RenderRouterWithContext(It.Is<bool>(y => y == false), It.Is<bool>(z => z == true), null), Times.Once);
		}

		[Fact]
		public void ShouldModifyStatusCode()
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns("{ status: 200 }");

			var htmlHelperMock = new HtmlHelperMocks();

			HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/"
			);
			htmlHelperMock.httpResponse.VerifySet(x => x.StatusCode = 200);
		}

		[Fact]
		public void ShouldRunCustomContextHandler()
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns("{ status: 200 }");

			var htmlHelperMock = new HtmlHelperMocks();

			HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/",
				contextHandler: (response, context) => response.StatusCode = context.status.Value
			);
			htmlHelperMock.httpResponse.VerifySet(x => x.StatusCode = 200);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldHandleQueryParams(bool withOverriddenPath)
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns("{ status: 200 }");

			var requestMock = new Mock<HttpRequestBase>();
			requestMock.SetupGet(x => x.Path).Returns("/test");
			var queryStringMock = new Mock<NameValueCollection>();
			queryStringMock.Setup(x => x.ToString()).Returns("?a=1&b=2");
			requestMock.SetupGet(x => x.QueryString).Returns(queryStringMock.Object);

			var htmlHelperMock = new HtmlHelperMocks(requestMock.Object);

			var result = HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: withOverriddenPath ? "/test2?b=1&c=2" : null,
				contextHandler: (response, context) => response.StatusCode = context.status.Value
			);

			htmlHelperMock.httpResponse.VerifySet(x => x.StatusCode = 200);

			if (withOverriddenPath)
			{
				mocks.Engine.Verify(x => x.Evaluate<string>(@"ReactDOMServer.renderToString(React.createElement(ComponentName, Object.assign({}, { location: ""/test2?b=1&c=2"", context: context })))"));
			}
			else
			{
				mocks.Engine.Verify(x => x.Evaluate<string>(@"ReactDOMServer.renderToString(React.createElement(ComponentName, Object.assign({}, { location: ""/test?a=1&b=2"", context: context })))"));
			}
		}

		[Theory]
		[InlineData("?a='\"1", "?a='\\\"1")]
		public void ShouldEscapeQuery(string query, string expected)
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns("{ status: 200 }");

			var requestMock = new Mock<HttpRequestBase>();
			requestMock.SetupGet(x => x.Path).Returns("/test");
			var queryStringMock = new Mock<NameValueCollection>();
			queryStringMock.Setup(x => x.ToString()).Returns(query);
			requestMock.SetupGet(x => x.QueryString).Returns(queryStringMock.Object);

			var htmlHelperMock = new HtmlHelperMocks(requestMock.Object);

			var result = HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: null,
				contextHandler: (response, context) => response.StatusCode = context.status.Value
			);

			htmlHelperMock.httpResponse.VerifySet(x => x.StatusCode = 200);

			mocks.Engine.Verify(x => x.Evaluate<string>(@"ReactDOMServer.renderToString(React.createElement(ComponentName, Object.assign({}, { location: ""/test" + expected + @""", context: context })))"));
		}

		[Fact]
		public void ShouldRedirectPermanent()
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns(@"{ status: 301, url: ""/foo"" }");

			var htmlHelperMock = new HtmlHelperMocks();

			HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/"
			);
			htmlHelperMock.httpResponse.Verify(x => x.RedirectPermanent(It.IsAny<string>()));
		}

		[Fact]
		public void ShouldRedirectWithJustUrl()
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns(@"{ url: ""/foo"" }");

			var htmlHelperMock = new HtmlHelperMocks();

			HtmlHelperExtensions.ReactRouter(
				htmlHelper: htmlHelperMock.htmlHelper.Object,
				componentName: "ComponentName",
				props: new { },
				path: "/"
			);
			htmlHelperMock.httpResponse.Verify(x => x.Redirect(It.IsAny<string>()));
		}

		[Fact]
		public void ShouldFailRedirectWithNoUrl()
		{
			var mocks = ConfigureMockReactEnvironment();
			ConfigureMockConfiguration();
			ConfigureReactIdGenerator();

			mocks.Engine.Setup(x => x.Evaluate<string>("JSON.stringify(context);"))
						.Returns("{ status: 301 }");

			var htmlHelperMock = new HtmlHelperMocks();

			Assert.Throws<ReactRouterException>(() =>

				HtmlHelperExtensions.ReactRouter(
					htmlHelper: htmlHelperMock.htmlHelper.Object,
					componentName: "ComponentName",
					props: new { },
					path: "/"
				)
			);
		}
	}
}
#endif
