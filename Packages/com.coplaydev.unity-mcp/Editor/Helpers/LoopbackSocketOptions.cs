using System;
using System.Net.Sockets;
#if UNITY_EDITOR_OSX
using System.Runtime.InteropServices;
#endif

namespace MCPForUnity.Editor.Helpers
{
    // 포트 검사와 실제 리스너가 같은 정책을 사용한다.
    internal static class LoopbackSocketOptions
    {
        public static void Configure(Socket socket)
        {
            socket.ExclusiveAddressUse = true;
#if UNITY_EDITOR_OSX
            // 이 Unity Mono의 관리형 ReuseAddress는 살아 있는 리스너까지 공유했다.
            // macOS 헤더의 네이티브 옵션을 따로 설정해 TIME_WAIT 주소만 재사용하고,
            // 다른 에디터/프로세스와 같은 포트를 동시에 listen하는 것은 금지한다.
            SetNative(socket, 0x0004, 1); // SO_REUSEADDR
            SetNative(socket, 0x0200, 0); // SO_REUSEPORT
#endif
        }

#if UNITY_EDITOR_OSX
        [DllImport("libc", SetLastError = true)]
        private static extern int setsockopt(int descriptor, int level, int option,
            ref int value, int length);

        private static void SetNative(Socket socket, int option, int value)
        {
            const int SolSocket = 0xffff;
            if (setsockopt(socket.Handle.ToInt32(), SolSocket, option, ref value, sizeof(int)) != 0)
                throw new SocketException(Marshal.GetLastWin32Error());
        }
#endif
    }
}
