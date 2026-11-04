using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Admin.Controllers;

[TestFixture]
public class ProductControllerListTests
{
    [Test]
    public void List_StaysParameterlessSoPluginOverridesStillBind()
    {
        var listMethods = typeof(ProductController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(ProductController.List))
            .ToList();

        listMethods.Should().ContainSingle();
        listMethods[0].GetParameters().Should().BeEmpty();
        listMethods[0].IsVirtual.Should().BeTrue();
    }

    [Test]
    public async Task List_PrefillsProductNameFromQuery()
    {
        ProductSearchModel passed = null;
        var result = await ListWithQuery("?q=%20Build%20your%20own%20computer%20", model => passed = model);

        var model = ViewModel(result);
        model.SearchProductName.Should().Be("Build your own computer");
        passed.Should().BeSameAs(model);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("?q=")]
    [TestCase("?q=%20%20")]
    public async Task List_WithoutKeyword_LeavesProductNameEmpty(string query)
    {
        var result = await ListWithQuery(query, _ => { });

        ViewModel(result).SearchProductName.Should().BeNull();
    }

    private static async Task<IActionResult> ListWithQuery(string query, Action<ProductSearchModel> onPrepare)
    {
        var factory = new Mock<IProductModelFactory>();
        factory.Setup(service => service.PrepareProductSearchModelAsync(It.IsAny<ProductSearchModel>()))
            .ReturnsAsync((ProductSearchModel model) =>
            {
                onPrepare(model);
                return model;
            });

        var controller = CreateController(factory.Object);
        var httpContext = new DefaultHttpContext();
        if (query != null)
            httpContext.Request.QueryString = new QueryString(query);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return await controller.List();
    }

    private static ProductSearchModel ViewModel(IActionResult result)
    {
        return result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<ProductSearchModel>().Subject;
    }

    private static ProductController CreateController(IProductModelFactory productModelFactory)
    {
        var constructor = typeof(ProductController).GetConstructors().Single();
        var arguments = constructor.GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(IProductModelFactory))
                return productModelFactory;

            if (parameter.ParameterType.IsInterface || parameter.ParameterType.IsAbstract)
                return ((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(parameter.ParameterType))!).Object;

            return Activator.CreateInstance(parameter.ParameterType);
        }).ToArray();

        return (ProductController)constructor.Invoke(arguments);
    }
}
