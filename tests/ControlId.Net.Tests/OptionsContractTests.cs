using System.Threading;

namespace ControlId.Tests;

public class OptionsContractTests
{
    private static ControlIdDeviceOptions Options() => new() { BaseAddress = new Uri("http://10.0.0.5") };

    [Fact]
    public void Defaults_follow_the_contract()
    {
        var options = Options();

        Assert.Equal(1, ControlIdDeviceOptions.DefaultMaxConcurrentRequests);
        Assert.Equal(1, options.MaxConcurrentRequests);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ConnectTimeout);
        Assert.True(options.ValidateImages);
        Assert.Equal(1920, options.MaxImageLongSide);
        Assert.Equal(1080, options.MaxImageShortSide);
        Assert.Equal(2 * 1024 * 1024, options.MaxImageBytes);
        Assert.Equal(1024 * 1024, options.MaxImageBatchBytes);
        Assert.Equal(100, options.MaxImagesPerBatch);
        Assert.Equal(200, options.MaxObjectsPerRequest);
        Assert.Equal(ExceptionResponseBody.ErrorOnly, options.ExceptionResponseBody);
        options.Validate();
    }

    [Theory]
    [InlineData(nameof(ControlIdDeviceOptions.ConnectTimeout))]
    [InlineData(nameof(ControlIdDeviceOptions.MaxImageLongSide))]
    [InlineData(nameof(ControlIdDeviceOptions.MaxImageShortSide))]
    [InlineData(nameof(ControlIdDeviceOptions.MaxImageBytes))]
    [InlineData(nameof(ControlIdDeviceOptions.MaxImageBatchBytes))]
    [InlineData(nameof(ControlIdDeviceOptions.MaxImagesPerBatch))]
    [InlineData(nameof(ControlIdDeviceOptions.MaxObjectsPerRequest))]
    [InlineData(nameof(ControlIdDeviceOptions.ExceptionResponseBody))]
    public void Out_of_range_values_are_rejected(string property)
    {
        var options = Options();
        switch (property)
        {
            case nameof(ControlIdDeviceOptions.ConnectTimeout): options.ConnectTimeout = TimeSpan.Zero; break;
            case nameof(ControlIdDeviceOptions.MaxImageLongSide): options.MaxImageLongSide = 0; break;
            case nameof(ControlIdDeviceOptions.MaxImageShortSide): options.MaxImageShortSide = options.MaxImageLongSide + 1; break;
            case nameof(ControlIdDeviceOptions.MaxImageBytes): options.MaxImageBytes = 0; break;
            case nameof(ControlIdDeviceOptions.MaxImageBatchBytes): options.MaxImageBatchBytes = 0; break;
            case nameof(ControlIdDeviceOptions.MaxImagesPerBatch): options.MaxImagesPerBatch = 0; break;
            case nameof(ControlIdDeviceOptions.MaxObjectsPerRequest): options.MaxObjectsPerRequest = 0; break;
            case nameof(ControlIdDeviceOptions.ExceptionResponseBody): options.ExceptionResponseBody = (ExceptionResponseBody)99; break;
        }

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        Assert.Equal(property, ex.ParamName);
    }

    [Fact]
    public void ConnectTimeout_above_Timeout_is_accepted()
    {
        var options = Options();
        options.Timeout = TimeSpan.FromSeconds(1);
        options.ConnectTimeout = TimeSpan.FromSeconds(5);

        options.Validate();
    }

    [Fact]
    public void Infinite_ConnectTimeout_is_accepted()
    {
        var options = Options();
        options.ConnectTimeout = Timeout.InfiniteTimeSpan;

        options.Validate();
    }

    [Fact]
    public void Outcome_exceptions_carry_their_data()
    {
        var unknown = new ControlIdOutcomeUnknownException("sem resposta", "create_objects", timedOut: true);
        Assert.Equal("create_objects", unknown.Command);
        Assert.True(unknown.TimedOut);

        var notSupported = new ControlIdNotSupportedException("load_objects", "6.9.12", System.Net.HttpStatusCode.NotFound, "Invalid command", null);
        Assert.Equal("load_objects", notSupported.Command);
        Assert.Equal("6.9.12", notSupported.Firmware);
        Assert.Equal("Invalid command", notSupported.DeviceMessage);
        Assert.IsAssignableFrom<ControlIdDeviceErrorException>(notSupported);

        var image = new ControlIdImageRejectedException(ImageRejectionReason.Size, 7, 10, 20, 3000, "grande");
        Assert.Equal(ImageRejectionReason.Size, image.Reason);
        Assert.Equal(7, image.UserId);
        Assert.Equal(3000, image.ByteCount);
    }
}
