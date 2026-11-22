using TeachingAssistant.Domain.Sandbox;

namespace TeachingAssistant.Tests.Domain;

public class JavaCompileUnitsTests
{
    [Fact]
    public void FilesOfOnePackageTree_AreOneProgram_FromTheFolderThePackagesStartIn()
    {
        var units = JavaCompileUnits.Plan(
        [
            new JavaSourceFile { Path = "src/shop/Main.java", Text = "package shop;\nclass Main {}" },
            new JavaSourceFile { Path = "src/shop/model/Item.java", Text = "package shop.model;\nclass Item {}" },
            new JavaSourceFile { Path = "README.md", Text = "not code" },
        ]);

        var unit = Assert.Single(units);
        Assert.Equal("src", unit.Root);
        Assert.Equal(["src/shop/Main.java", "src/shop/model/Item.java"], unit.Files);
    }

    [Fact]
    public void TasksSideBySideWithoutPackages_AreSeparatePrograms()
    {
        var units = JavaCompileUnits.Plan(
        [
            new JavaSourceFile { Path = "task1/Main.java", Text = "public class Main {}" },
            new JavaSourceFile { Path = "task1/Helper.java", Text = "class Helper {}" },
            new JavaSourceFile { Path = "task2/Main.java", Text = "public class Main {}" },
        ]);

        Assert.Equal(["task1", "task2"], units.Select(u => u.Root));
        Assert.Equal(["task1/Helper.java", "task1/Main.java"], units[0].Files);
    }

    [Theory]
    [InlineData("package shop;\nclass A {}", "shop")]
    [InlineData("/* package wrong; */\n// package wrong2;\npackage  ge.kiu . zoo ;\nclass A {}", "ge.kiu.zoo")]
    [InlineData("import java.util.List;\nclass A {}", null)]
    [InlineData("class A { String s = \"package x;\"; }", null)]
    public void PackageOf_ReadsTheDeclaration_IgnoringComments(string text, string? expected)
    {
        Assert.Equal(expected, JavaCompileUnits.PackageOf(text));
    }

    [Theory]
    [InlineData("shop/Main.java", "shop", "")]
    [InlineData("project/src/ge/kiu/Main.java", "ge.kiu", "project/src")]
    [InlineData("homework/Main.java", "shop", "homework")]
    [InlineData("Main.java", null, "")]
    [InlineData(@"src\shop\Main.java", "shop", "src")]
    public void RootOf_DropsThePackageFolders_OrKeepsTheFileFolderWhenTheyDoNotMatch(string path, string? package, string expected)
    {
        Assert.Equal(expected, JavaCompileUnits.RootOf(path, package));
    }
}
