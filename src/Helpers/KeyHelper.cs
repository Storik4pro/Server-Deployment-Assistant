using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerDeploymentAssistant
{
    public struct KeyMapping
    {
        public byte VkCode;
        public uint ScanCode;
        public byte ShiftState;
    }
    public static class KeyHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern short VkKeyScanEx(char ch, IntPtr dwhkl);
        [DllImport("user32.dll")]
        static extern bool UnloadKeyboardLayout(IntPtr hkl);
        [DllImport("user32.dll")]
        static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
        public const uint MAPVK_VK_TO_VSC = 0;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MapVirtualKeyEx(
            uint uCode,
            uint uMapType,
            IntPtr dwhkl
        );
        public class KeyboardPointer : IDisposable
        {
            private readonly IntPtr pointer;
            public KeyboardPointer(int klid)
            {
                pointer = LoadKeyboardLayout(klid.ToString("X8"), 1);
            }
            public KeyboardPointer(CultureInfo culture)
              : this(culture.KeyboardLayoutId) { }
            public void Dispose()
            {
                UnloadKeyboardLayout(pointer);
                GC.SuppressFinalize(this);
            }
            ~KeyboardPointer()
            {
                UnloadKeyboardLayout(pointer);
            }
            // Converting to System.Windows.Forms.Key here, but
            // some other enumerations for similar tasks have the same
            // one-to-one mapping to the underlying Windows API values
            public bool GetKey(char character, out Keys key)
            {
                short keyNumber = VkKeyScanEx(character, pointer);
                if (keyNumber == -1)
                {
                    key = System.Windows.Forms.Keys.None;
                    return false;
                }
                key = (System.Windows.Forms.Keys)(((keyNumber & 0xFF00) << 8) | (keyNumber & 0xFF));
                return true;
            }
        }
        private static string DescribeKey(Keys key)
        {
            StringBuilder desc = new StringBuilder();
            if ((key & Keys.Shift) != Keys.None)
                desc.Append("Shift: ");
            if ((key & Keys.Control) != Keys.None)
                desc.Append("Control: ");
            if ((key & Keys.Alt) != Keys.None)
                desc.Append("Alt: ");
            return desc.Append(key & Keys.KeyCode).ToString();
        }


        public static void TestKeys()
        {
            string testChars = "123qweÚÁOáá";
            Keys key;
            foreach (var culture in (new string[] { "he-IL", "en-US", "en-IE", "hu-HU" }).Select(code => CultureInfo.GetCultureInfo(code)))
            {
                Console.WriteLine(culture.Name);
                using (var keyboard = new KeyboardPointer(culture))
                    foreach (char test in testChars)
                    {
                        Console.Write(test);
                        Console.Write('\t');
                        if (keyboard.GetKey(test, out key))
                            Console.WriteLine(DescribeKey(key));
                        else
                            Console.WriteLine("No Key");
                    }
            }
           // Console.Read();//Stop window closing
        }

        private const uint KLF_NOTELLSHELL = 0x00000080;

        public static KeyMapping GetVirtualKey(char ch, string langCode = "en-US") 
        {
            var culture = CultureInfo.CreateSpecificCulture(langCode);
            string layoutHex = $"0000{culture.LCID:X4}";
            IntPtr hkl = LoadKeyboardLayout(layoutHex, KLF_NOTELLSHELL);
            if (hkl == IntPtr.Zero)
                throw new InvalidOperationException("[KEYFINDER] Key mapping error " + langCode);

            short vkScan = VkKeyScanEx(ch, hkl);
            if (vkScan == -1)
                throw new InvalidOperationException($"[KEYFINDER] Symbol '{ch}' is not supported in mapping {langCode}");

            byte vkCode = (byte)(vkScan & 0xFF);
            byte shiftState = (byte)((vkScan >> 8) & 0xFF);

            uint scanCode = MapVirtualKeyEx(vkCode, MAPVK_VK_TO_VSC, hkl);

            return new KeyMapping
            {
                VkCode = vkCode,
                ScanCode = scanCode,
                ShiftState = shiftState
            };
        }
    }
}
