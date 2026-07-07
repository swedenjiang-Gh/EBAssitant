namespace EBAssistant.Tests;

internal static class ComInvocationTests
{
    public static void Run()
    {
        AllowsVoidMethodReturningNull();
        RequiresObjectMethodToReturnValue();
        UnwrapsInvocationExceptions();
    }

    private static void AllowsVoidMethodReturningNull()
    {
        var target = new SampleComLikeObject();

        ComInvocation.CallVoid(target, nameof(SampleComLikeObject.Copy));

        Assert.True(target.Copied);
    }

    private static void RequiresObjectMethodToReturnValue()
    {
        try
        {
            ComInvocation.Call(new SampleComLikeObject(), nameof(SampleComLikeObject.ReturnsNull));
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Expected null object-returning call to fail.");
    }

    private static void UnwrapsInvocationExceptions()
    {
        try
        {
            ComInvocation.CallVoid(new SampleComLikeObject(), nameof(SampleComLikeObject.ThrowComLikeError));
        }
        catch (InvalidOperationException ex)
        {
            Assert.True(ex.Message.Contains("ThrowComLikeError"));
            Assert.True(ex.Message.Contains("inner failure"));
            return;
        }

        throw new InvalidOperationException("Expected invocation error to be unwrapped.");
    }

    public sealed class SampleComLikeObject
    {
        public bool Copied { get; private set; }

        public void Copy()
        {
            Copied = true;
        }

        public SampleComLikeObject ReturnsNull() => null!;

        public void ThrowComLikeError()
        {
            throw new InvalidOperationException("inner failure");
        }
    }
}
