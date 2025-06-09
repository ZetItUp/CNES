using CNES.Utils;
using System;
using System.Diagnostics;

namespace CNES.Core
{
    public class CPU6502
    {
        // Flags Helper Methods
        private void SetFlag(byte mask) => Status |= mask;
        private void ClearFlag(byte mask) => Status &= (byte)~mask;
        private bool IsFlagSet(byte mask) => (Status & mask) != 0;

        // Registers
        public byte A;      // Accumulator
        public byte X;      // Index Register X
        public byte Y;      // Index Register Y
        public byte SP;     // Stack Pointer
        public ushort PC;   // Program Counter
        public byte Status; // Processor Status Register

        private MemoryBus memoryBus;

        // Immediate addressing - just reads the next byte
        private byte Immediate() => memoryBus.Read(PC++);

        // Zero Page addressing - 8-bit address
        private ushort ZeroPage() => memoryBus.Read(PC++);

        // Absolute addressing - full 16-bit address
        private ushort Absolute()
        {
            byte lo = memoryBus.Read(PC++);
            byte hi = memoryBus.Read(PC++);
            return (ushort)((hi << 8) | lo);
        }


        public CPU6502(MemoryBus memBus) 
        { 
            memoryBus = memBus;
        }

        public void Reset()
        {
            // Reset CPU State
            SP = 0xFD;      // Stack Pointer starts at 0xFD as of NES reset behaviour

            // Clear Registers
            A = 0;
            X = 0;
            Y = 0;

            Status = 0x24;  // Default flag status: IRQ disabled, unused flag set

            // Read reset vector, 0xFFFC and 0xFFFD, to get initial PC
            byte lo = memoryBus.Read(0xFFFC);
            byte hi = memoryBus.Read(0xFFFD);
            PC = (ushort)((hi << 8) | lo);
        }

        // One CPU step
        public void Step()
        {
            // Fetch opcode at current PC
            byte opcode = memoryBus.Read(PC++);

            Logger.DebugLog($"[CPU] PC: 0x{PC:X4} Opcode: 0x{opcode:X2}");

            switch(opcode)
            {
                case 0xA9:  // LDA Immediate
                    {
                        // Read the next byte as the immediate value
                        byte value = memoryBus.Read(PC++);

                        // Load the value into the accumulator
                        A = value;

                        // Update zero and negative flags based on the A value
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA 0x{value:X2} => Reg A is set to: 0x{A:X2}");
                        break;
                    }
                case 0x8D:  // STA Absolute
                    {
                        // Read the low byte and high byte of the target address
                        ushort addr = Absolute();

                        // Store the value of the accumulator at the given address
                        memoryBus.Write(addr, A);

                        Logger.DebugLog($"STA 0x{addr:X4} <= A ({A:X2})");
                        break;
                    }
                case 0xA2:  // LDX Immediate
                    {
                        // Read value from bus
                        byte value = memoryBus.Read(PC++);

                        // Load X register with value
                        X = value;

                        // Update zero and negative flags based on the X value
                        SetZeroAndNegativeFlags(X);

                        Logger.DebugLog($"LDX 0x{value:X2} => Reg X is set to: 0x{X:X2}");
                        break;
                    }
                case 0xE8:  // INX Implied
                    {
                        // Increment X register by 1
                        X++;

                        // Update zero and negative flags based on the X value
                        SetZeroAndNegativeFlags(X);

                        Logger.DebugLog($"INX => X is 0x{X:X2}");
                        break;
                    }
                case 0x4C:  // JMP Absolute
                    {
                        // Read low and high bytes of jump address
                        byte lo = memoryBus.Read(PC++);
                        byte hi = memoryBus.Read(PC++);

                        // Combine bytes into a 16-bit address
                        ushort addr = (ushort)((hi << 8) | lo);

                        // Set PC to the new address
                        PC = addr;

                        Logger.DebugLog($"JMP to 0x{addr:X4}");
                        break;
                    }
                case 0xEA:  // NOP 
                    {
                        // No operation
                        Logger.DebugLog($"NOP");
                        break;
                    }
                case 0x00:  // BRK (Force Interrupt)
                    {
                        //TODO: Implement interruption
                        Logger.DebugLog($"BRK - Interrupt here...");
                        break;
                    }
                case 0x78:  // SEI Set Interrupt Disable
                    {
                        // Set bit 2  in the status register (Interrupt Disable Flag)
                        SetFlag(0x04);

                        Logger.DebugLog("SEI - Interrupts Disabled");
                        break;
                    }
                case 0x69:  // ADC Immediate
                    {
                        // Add immediate value + carry flag to accumulator
                        byte value = memoryBus.Read(PC++);
                        int carry = (Status & 0x01) != 0 ? 1 : 0;
                        int sum = A + value + carry;

                        // Set CF if sum > 255
                        if(sum > 0xFF)
                        {
                            Status |= 0x01;
                        }
                        else
                        {
                            Status &= 0xFE;
                        }

                        // Set overflow flag (signed)
                        bool overflow = (~(A ^ value) & (A ^ sum) & 0x80) != 0;

                        if(overflow)
                        {
                            Status |= 0x40;
                        }
                        else
                        {
                            Status &= 0xBF;
                        }

                        A = (byte)(sum & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ADC 0x{value:X2} => A is 0x{A:X2}");
                        break;
                    }
                case 0x29:  // AND Immediate
                    {
                        // Bitwise and accumulator with immediate value
                        byte value = memoryBus.Read(PC++);
                        A = (byte)(A & value);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"AND 0x{value:X2} => A is 0x{A:X2}");
                        break;
                    }
                case 0xC9:  // CMP Immediate
                    {
                        // Compare accumulator with immediate value
                        byte value = memoryBus.Read(PC++);
                        int result = A - value;

                        // Set Carry if A => value
                        if(A >= value)
                        {
                            SetFlag(0x01);
                        }
                        else
                        {
                            ClearFlag(0x01);
                        }

                        SetZeroAndNegativeFlags((byte)(result & 0xFF));

                        Logger.DebugLog($"CMP 0x{value:X2} => A is 0x{A:X2}, Flags updated");
                        break;
                    }
                case 0x58:  // CLI - Clear Interrupt Disable
                    {
                        // Clear bit 2 in the status register (Interrupt Disable Flag)
                        ClearFlag(0x04);

                        Logger.DebugLog($"CLI - Interrputs Enabled");
                        break;
                    }
                case 0xB8:  // CLV - Clear Overflow Flag
                    {
                        // Clear bit 6 in the status register (Overflow flag)
                        ClearFlag(0x40);

                        Logger.DebugLog($"CLV - Overflow Flag Cleared");
                        break;
                    }
                case 0x40:  // RTI - Return from Interrupt
                    {
                        // TODO: Implement
                        Logger.DebugLog($"RTI - Return from Interrupt (NYI)");
                        break;
                    }
                default:
                    {
                        Logger.ErrorLog($"[CPU] Unknown Opcode: 0x{opcode:X2} at PC: {PC - 1:X4}");
                        break;
                    }
            }
        }

        private void SetZeroAndNegativeFlags(byte result)
        {
            // Set or clear Zero flag (Bit 1), Set if result == 0
            if(result== 0)
            {
                Status |= 0x02;     // Set zero flag
            }
            else
            {
                Status &= 0xFD;     // Clear Zero Flag
            }

            // Set or clear Negative flag (Bit 7), Set if Bit 7 of result is 1
            if((result & 0x80) != 0)
            {
                Status |= 0x80;     // Set negative flag
            }
            else
            {
                Status &= 0x7F;     // Clear negative flag
            }
        }
    }
}
