using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Api.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// No database connection, container, environment mutation or production override.
public sealed class MysqlFixtureConfigurationTests
{
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "fixture-regression";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData(13306)]
    [InlineData(24381)]
    public void LocalFixtureUsesItsActualDatabasePortWithoutWeakeningPolicy(int port)
    {
        var settings = new Dictionary<string,string?>
        {
            ["Deployment:Mode"] = "LocalTest", ["Deployment:LocalTestDatabasePort"] = port.ToString(),
            ["ConnectionStrings:DefaultConnection"] = $"Server=127.0.0.1;Port={port};Database=electronic_register_test;User ID=root;Password=synthetic",
            ["urls"] = "http://127.0.0.1:0", ["Jwt:Key"] = new string('k',48),
            ["Jwt:Issuer"] = "fixture", ["Jwt:Audience"] = "fixture-client"
        };
        Assert.True(RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new EnvironmentStub()).Local);
        settings["Deployment:LocalTestDatabasePort"] = "3306";
        Assert.Throws<InvalidOperationException>(() => RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new EnvironmentStub()));
        settings["Deployment:LocalTestDatabasePort"] = port.ToString();
        settings["ConnectionStrings:DefaultConnection"] = $"Server=127.0.0.1;Port={port};Database=testdb;User ID=root;Password=synthetic";
        Assert.Throws<InvalidOperationException>(() => RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new EnvironmentStub()));
    }

    [Theory]
    [InlineData("Server=127.0.0.1;Port=24381;Database=electronic_register_test;User ID=root")]
    [InlineData("Server=127.0.0.1;Port=13306;Database=electronic_register_test;User ID=root")]
    public void ConnectionStringsCannotAuthorizeTheMysql84FixtureBranch(string connection)
        => Assert.False(Mysql84FixtureTarget.IsOwned(connection));
}
