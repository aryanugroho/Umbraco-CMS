// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.Models;
using Umbraco.Core.Diagnostics;
using Umbraco.Core.Hosting;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence;
using Umbraco.Core.Serialization;
using Umbraco.Core.Strings;
using Umbraco.Net;
using Umbraco.Tests.Common.TestHelpers;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Umbraco.Tests.Common
{
    /// <summary>
    /// Common helper properties and methods useful to testing
    /// </summary>
    public abstract class TestHelperBase
    {
        private readonly ITypeFinder _typeFinder;
        private UriUtility _uriUtility;
        private IIOHelper _ioHelper;
        private string _workingDir;

        protected TestHelperBase(Assembly entryAssembly)
        {
            MainDom = new SimpleMainDom();
            _typeFinder = new TypeFinder(NullLoggerFactory.Instance.CreateLogger<TypeFinder>(), new DefaultUmbracoAssemblyProvider(entryAssembly, NullLoggerFactory.Instance), new VaryingRuntimeHash());
        }

        public ITypeFinder GetTypeFinder() => _typeFinder;

        public TypeLoader GetMockedTypeLoader() =>
            new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<IAppPolicyCache>(), new DirectoryInfo(GetHostingEnvironment().MapPathContentRoot(Constants.SystemDirectories.TempData)), Mock.Of<ILogger<TypeLoader>>(), Mock.Of<IProfilingLogger>());

        //// public Configs GetConfigs() => GetConfigsFactory().Create();

        public abstract IBackOfficeInfo GetBackOfficeInfo();

        //// public IConfigsFactory GetConfigsFactory() => new ConfigsFactory();

        /// <summary>
        /// Gets the working directory of the test project.
        /// </summary>
        public virtual string WorkingDirectory
        {
            get
            {
                if (_workingDir != null)
                {
                    return _workingDir;
                }

                var dir = Path.Combine(Assembly.GetExecutingAssembly().GetRootDirectorySafe(), "TEMP");

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _workingDir = dir;
                return _workingDir;
            }
        }

        public IShortStringHelper ShortStringHelper { get; } = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());

        public IJsonSerializer JsonSerializer { get; } = new JsonNetSerializer();

        public IVariationContextAccessor VariationContextAccessor { get; } = new TestVariationContextAccessor();

        public abstract IDbProviderFactoryCreator DbProviderFactoryCreator { get; }

        public abstract IBulkSqlInsertProvider BulkSqlInsertProvider { get; }

        public abstract IMarchal Marchal { get; }

        public CoreDebugSettings CoreDebugSettings { get; } = new CoreDebugSettings();

        public IIOHelper IOHelper
        {
            get
            {
                if (_ioHelper == null)
                {
                    IHostingEnvironment hostingEnvironment = GetHostingEnvironment();

                    if (TestEnvironment.IsWindows)
                    {
                        _ioHelper = new IOHelperWindows(hostingEnvironment);
                    }
                    else if (TestEnvironment.IsLinux)
                    {
                        _ioHelper = new IOHelperLinux(hostingEnvironment);
                    }
                    else if (TestEnvironment.IsOSX)
                    {
                        _ioHelper = new IOHelperOSX(hostingEnvironment);
                    }
                    else
                    {
                        throw new NotSupportedException("Unexpected OS");
                    }
                }

                return _ioHelper;
            }
        }

        public IMainDom MainDom { get; }

        public UriUtility UriUtility
        {
            get
            {
                if (_uriUtility == null)
                {
                    _uriUtility = new UriUtility(GetHostingEnvironment());
                }

                return _uriUtility;
            }
        }

        /// <summary>
        /// Some test files are copied to the /bin (/bin/debug) on build, this is a utility to return their physical path based on a virtual path name
        /// </summary>
        public virtual string MapPathForTestFiles(string relativePath)
        {
            if (!relativePath.StartsWith("~/"))
            {
                throw new ArgumentException("relativePath must start with '~/'", nameof(relativePath));
            }

            var codeBase = typeof(TestHelperBase).Assembly.CodeBase;
            var uri = new Uri(codeBase);
            var path = uri.LocalPath;
            var bin = Path.GetDirectoryName(path);

            return relativePath.Replace("~/", bin + "/");
        }

        public IUmbracoVersion GetUmbracoVersion() => new UmbracoVersion();

        public IServiceCollection GetRegister() => new ServiceCollection();

        public abstract IHostingEnvironment GetHostingEnvironment();

        public abstract IApplicationShutdownRegistry GetHostingEnvironmentLifetime();

        public abstract IIpResolver GetIpResolver();

        public IRequestCache GetRequestCache() => new DictionaryAppCache();

        public IPublishedUrlProvider GetPublishedUrlProvider()
        {
            var mock = new Mock<IPublishedUrlProvider>();

            return mock.Object;
        }

        public ILoggingConfiguration GetLoggingConfiguration(IHostingEnvironment hostingEnv = null)
        {
            hostingEnv = hostingEnv ?? GetHostingEnvironment();
            return new LoggingConfiguration(
                Path.Combine(hostingEnv.ApplicationPhysicalPath, "umbraco", "logs"));
        }
    }
}
