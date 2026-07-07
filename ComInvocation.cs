using System.Reflection;

namespace EBAssistant;

public static class ComInvocation
{
    public static object Get(object target, string name)
    {
        try
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null)
                ?? throw new InvalidOperationException("无法读取 COM 属性：" + name);
        }
        catch (TargetInvocationException ex)
        {
            throw CreateInvocationException("读取 COM 属性", name, ex);
        }
    }

    public static void Set(object target, string name, object value)
    {
        try
        {
            target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, [value]);
        }
        catch (TargetInvocationException ex)
        {
            throw CreateInvocationException("设置 COM 属性", name, ex);
        }
    }

    public static object Call(object target, string name, params object[] args)
    {
        try
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args)
                ?? throw new InvalidOperationException("无法调用 COM 方法：" + name);
        }
        catch (TargetInvocationException ex)
        {
            throw CreateInvocationException("调用 COM 方法", name, ex);
        }
    }

    public static void CallVoid(object target, string name, params object[] args)
    {
        try
        {
            target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
        }
        catch (TargetInvocationException ex)
        {
            throw CreateInvocationException("调用 COM 方法", name, ex);
        }
    }

    private static InvalidOperationException CreateInvocationException(
        string operation,
        string name,
        TargetInvocationException exception)
    {
        var inner = exception.InnerException;
        var detail = inner is null ? exception.Message : inner.Message;
        return new InvalidOperationException($"{operation}失败：{name}。{detail}", inner ?? exception);
    }
}
