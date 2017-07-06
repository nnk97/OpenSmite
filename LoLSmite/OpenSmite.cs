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
        private bool m_bFailed { get; set; }

        public COpenSmite(int _PID)
        {
            m_bFailed = false;
            m_iProcessId = _PID;
            m_hProcHandle = WinAPI.OpenProcess(WinAPI.ProcessAccessFlags.VirtualMemoryRead, false, _PID);
            new Thread(Logic).Start();
        }

        // Helperino
        T Read<T>(IntPtr Address)
        {
            int Size = Marshal.SizeOf(typeof(T));
            byte[] Bfr = new byte[Size];
            if (!WinAPI.ReadProcessMemory(m_hProcHandle, Address, Bfr, Size, 0))
            {
                m_bFailed = true;
                Console.WriteLine("Read memory failed!");
            }
            GCHandle handle = GCHandle.Alloc(Bfr, GCHandleType.Pinned);
            T Temp = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return Temp;
        }

        string GetSpellName(IntPtr pSpell)
        {
            IntPtr SpellData = Read<IntPtr>(pSpell + 0xF4);
            byte[] bfr = new byte[16];
            if (!WinAPI.ReadProcessMemory(m_hProcHandle, SpellData + 0x18, bfr, 16, 0))
            {
                m_bFailed = true;
                Console.WriteLine("GetSpellName failed!");
            }
            return Encoding.UTF8.GetString(bfr);
        }
        
        public void Logic()
        {
            Process Proc = Process.GetProcessById(m_iProcessId);
            IntPtr LoLBase = Proc.MainModule.BaseAddress;
            Random rng = new Random();

            // If game is currently loading, then wait for a while.
            while (Read<int>(LoLBase + 0x16A5CCC) < 2)  // Game state
                Thread.Sleep(1000);

            // Grab some data about our summoner spells
            /*  VKeyCodes: 0x44 = D, 0x46 = F
                0x1698B84 = LocalPlayer // NOTE: Game version 7.12
                SlotID: D = 4, F = 5
                0x1698B84 + 0x2F88 + 0x4 * iSlot = SpellAddress  */
            byte iSmiteKeyCode = 0x0;
            IntPtr LocalPlayer = Read<IntPtr>(LoLBase + 0x16A6AB0);
            IntPtr SpellPtr = IntPtr.Zero;
            {
                IntPtr SpellD = Read<IntPtr>(LocalPlayer + 0x2F88 + 0x4 * 4);
                IntPtr SpellF = Read<IntPtr>(LocalPlayer + 0x2F88 + 0x4 * 5);
                //Console.WriteLine($"SpellD: 0x{SpellD.ToString("X")} & SpellF: 0x{SpellF.ToString("X")}");
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
            while (!m_bFailed)
            {
                if (WinAPI.IsKeyPushedDown(iSmiteKeyCode))
                {
                    // Read "highlighted" object (the one under mouse) 
                    IntPtr Highlighted = Read<IntPtr>(LoLBase + 0x16A36FC);
                    if (Highlighted == IntPtr.Zero)
                        continue;

                    // Read current game time, compare to cooldown
                    float flGameTime = Read<float>(LoLBase + 0x16A452C);

                    // Read current spell cooldown
                    float flCooldownEnd = Read<float>(SpellPtr + 0x18);

                    // So, can we cast the spell?
                    if (flGameTime <= flCooldownEnd)
                        continue;

                    // Do we have the Smite? (cooldowns seems to be confusing here, so is spellstate flag... idk)
                    int iSmiteStacks = Read<int>(SpellPtr + 0x28);
                    if (iSmiteStacks == 0)
                        continue;

                    // Check target health to see if our Smite can kill it
                    float flTargetHealth = Read<float>(Highlighted + 0x650);
                    float flSmiteDamage = Read<float>(SpellPtr + 0x58);
                    if (flTargetHealth > flSmiteDamage)
                        continue;

                    // Send key event to active window (Let's assume that it's League)
                    //Thread.Sleep(rng.Next(5, 125));
                    WinAPI.keybd_event(iSmiteKeyCode, (byte)WinAPI.MapVirtualKey(iSmiteKeyCode, 0), WinAPI.KEYEVENTF_KEYUP, UIntPtr.Zero);

                    Console.WriteLine("Casting Smite!");

                    Thread.Sleep(250);
                }

                Thread.Sleep(15);
            }
        }
    }
}
