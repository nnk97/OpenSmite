using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LoLSmite
{
    public class COpenSmite
    {
        private IntPtr m_hProcHandle { get; set; }
        private int m_iProcessId { get; set; }

        public COpenSmite(int _PID)
        {
            m_iProcessId = _PID;
            m_hProcHandle = WinAPI.OpenProcess(WinAPI.ProcessAccessFlags.VirtualMemoryRead, false, _PID);
            new Thread(Logic).Start();
        }

        // Helperino
        int ReadInt(IntPtr Address)
        {
            byte[] bfr = new byte[4];
            if (!WinAPI.ReadProcessMemory(m_hProcHandle, Address, bfr, 4, 0))
                Console.WriteLine("ReadInt failed!");
            return BitConverter.ToInt32(bfr, 0);
        }

        IntPtr ReadIntPtr(IntPtr Address)
        {
            byte[] bfr = new byte[4];
            if (!WinAPI.ReadProcessMemory(m_hProcHandle, Address, bfr, 4, 0))
                Console.WriteLine("ReadIntPtr failed!");
            return (IntPtr)BitConverter.ToUInt32(bfr, 0);
        }

        float ReadFloat(IntPtr Address)
        {
            byte[] bfr = new byte[4];
            if (!WinAPI.ReadProcessMemory(m_hProcHandle, Address, bfr, 4, 0))
                Console.WriteLine("ReadFloat failed!");
            return BitConverter.ToSingle(bfr, 0);
        }

        string GetSpellName(IntPtr pSpell)
        {
            IntPtr SpellData = ReadIntPtr(pSpell + 0xF4);
            byte[] bfr = new byte[16];
            if (!WinAPI.ReadProcessMemory(m_hProcHandle, SpellData + 0x18, bfr, 16, 0))
                Console.WriteLine("GetSpellName failed!");
            return Encoding.UTF8.GetString(bfr);
        }

        // I guess you can read that from the game, but that's easier way (lazy)
        float[] flSmiteDamage = { 0.0f, 390f, 410f, 430f, 450f, 480f, 510f, 540f, 570f, 600f, 640f, 680f, 720f, 760f, 800f, 850f, 900f, 950f, 1000f };

        public void Logic()
        {
            Process Proc = Process.GetProcessById(m_iProcessId);
            IntPtr LoLBase = Proc.MainModule.BaseAddress;
            Random rng = new Random();

            // If game is currently loading, then wait for a while.
            while (ReadInt(LoLBase + 0x16A0220) < 2)
                Thread.Sleep(1000);

            // Grab some data about our summoner spells
            /*  VKeyCodes: 0x44 = D, 0x46 = F
                0x1698B84 = LocalPlayer
                SlotID: D = 4, F = 5
                0x1698B84 + 0x2F88 + 0x4 * iSlot = SpellAddress  */
            byte iSmiteKeyCode = 0x0;
            IntPtr LocalPlayer = ReadIntPtr(LoLBase + 0x1698B84);
            IntPtr SpellPtr = IntPtr.Zero;
            {
                IntPtr SpellD = ReadIntPtr(LocalPlayer + 0x2F88 + 0x4 * 4);
                IntPtr SpellF = ReadIntPtr(LocalPlayer + 0x2F88 + 0x4 * 5);
                if (GetSpellName(SpellD).Contains("Smite"))
                {
                    iSmiteKeyCode = 0x44;
                    SpellPtr = SpellD;
                }
                else if (GetSpellName(SpellF).Contains("Smite"))
                {
                    iSmiteKeyCode = 0x46;
                    SpellPtr = SpellF;
                }
            }

            if (SpellPtr == IntPtr.Zero)
            {
                Console.WriteLine("It looks like you didn't equip Smite summoner in this game.");
                WinAPI.CloseHandle(m_hProcHandle);
                return;
            }
            else
                Console.WriteLine($"Found Smite under {((iSmiteKeyCode == 0x44) ? 'D' : 'F')} slot!");

            // Main loop while user is in game.
            while (true)
            {
                if (WinAPI.IsKeyPushedDown(88)) // VK_X
                {
                    // Read "highlighted" object (the one under mouse) 
                    IntPtr Highlighted = ReadIntPtr(LoLBase + 0x169C3B0);
                    if (Highlighted.Equals(IntPtr.Zero))
                        continue;

                    // Read current game time, compare to cooldown
                    float flGameTime = ReadFloat(LoLBase + 0x169BE3C);

                    // Read current spell cooldown
                    float flCooldownEnd = ReadFloat(SpellPtr + 0x18);

                    // So, can we cast the spell?
                    if (flGameTime <= flCooldownEnd)
                        continue;

                    // Check our level to get the Smite damage
                    int iPlayerLevel = ReadInt(LocalPlayer + 0x3C5C);

                    // Check target health to see if our Smite can kill it
                    float flTargetHealth = ReadFloat(Highlighted + 0x650);
                    if (flTargetHealth > flSmiteDamage[iPlayerLevel])
                        continue;

                    // Send key event to active window (Let's assume that it's League)
                    WinAPI.keybd_event(iSmiteKeyCode, (byte)WinAPI.MapVirtualKey(iSmiteKeyCode, 0), WinAPI.KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                    Thread.Sleep(rng.Next(5, 125));
                    WinAPI.keybd_event(iSmiteKeyCode, (byte)WinAPI.MapVirtualKey(iSmiteKeyCode, 0), WinAPI.KEYEVENTF_KEYUP, UIntPtr.Zero);

                    Console.WriteLine($"Casted Smite (CD: {flGameTime - flCooldownEnd})");

                    Thread.Sleep(250);
                }

                Thread.Sleep(15);
            }
        }
    }
}
