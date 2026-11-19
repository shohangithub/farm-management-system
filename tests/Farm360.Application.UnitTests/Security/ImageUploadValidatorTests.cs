using System;
using System.IO;
using System.Text;
using Farm360.Application.Common.Files;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Security;

public sealed class ImageUploadValidatorTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 2];
    private static readonly byte[] Webp = [.. "RIFF"u8.ToArray(), 0x24, 0, 0, 0, .. "WEBPVP8 "u8.ToArray()];

    [Theory]
    [InlineData("photo.png", ".png")]
    [InlineData("PHOTO.PNG", ".png")]
    public void Png_IsAccepted(string name, string expected) =>
        ImageUploadValidator.Validate(new MemoryStream(Png), name).Should().Be(expected);

    [Fact]
    public void Jpeg_IsAccepted_WithCanonicalExtension() =>
        ImageUploadValidator.Validate(new MemoryStream(Jpeg), "cow.jpeg").Should().Be(".jpg");

    [Fact]
    public void Webp_IsAccepted() =>
        ImageUploadValidator.Validate(new MemoryStream(Webp), "cow.webp").Should().Be(".webp");

    [Fact]
    public void Validation_LeavesStreamAtStart()
    {
        var stream = new MemoryStream(Png);
        ImageUploadValidator.Validate(stream, "a.png");
        stream.Position.Should().Be(0);
    }

    [Theory]
    [InlineData("evil.svg")]
    [InlineData("evil.html")]
    [InlineData("evil.png.html")]
    [InlineData("noextension")]
    [InlineData("../../wwwroot/index.html")]
    public void DisallowedClientExtension_IsRejected_EvenWithImageBytes(string name)
    {
        var act = () => ImageUploadValidator.Validate(new MemoryStream(Png), name);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>")]
    [InlineData("GIF89a............")]
    public void NonImageContent_WithImageExtension_IsRejected(string content)
    {
        var act = () => ImageUploadValidator.Validate(new MemoryStream(Encoding.UTF8.GetBytes(content)), "photo.png");
        act.Should().Throw<ArgumentException>().WithMessage("*not a valid JPEG, PNG or WebP*");
    }

    [Fact]
    public void EmptyFile_IsRejected()
    {
        var act = () => ImageUploadValidator.Validate(new MemoryStream(), "photo.png");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OversizedFile_IsRejected()
    {
        var big = new byte[1024 + 1];
        Png.CopyTo(big, 0);
        var act = () => ImageUploadValidator.Validate(new MemoryStream(big), "photo.png", maxBytes: 1024);
        act.Should().Throw<ArgumentException>().WithMessage("*limit*");
    }
}
