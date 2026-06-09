using System;
using System.Text;

namespace EBAssist.Adapter2025
{
    internal static class Program
    {
        private static int Main()
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Write("{\"Success\":false,\"Message\":\"本机未注册 EB 2025 COM 32 类型库，无法启用 EB 2025 适配器。\",\"Data\":null}");
            return 1;
        }
    }
}
