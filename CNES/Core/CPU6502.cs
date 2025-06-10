using CNES.Utils;
using System;
using System.Diagnostics;

namespace CNES.Core
{
    public class CPU6502
    {
        private const byte FLAG_CARRY = 0x01;
        private const byte FLAG_ZERO = 0x02;
        private const byte FLAG_INTERRUPT = 0x04;
        private const byte FLAG_DECIMAL = 0x08;
        private const byte FLAG_BREAK = 0x10;
        private const byte FLAG_UNUSED = 0x20;
        private const byte FLAG_OVERFLOW = 0x40;
        private const byte FLAG_NEGATIVE = 0x80;

        // Stack Helper Methods
        // Push a byte onto the stack
        private void Push(byte value)
        {
            // Stack starts at 0x0100, so offset SP into the correct memory region
            memoryBus.Write((ushort)(0x0100 + SP), value);

            // Decrease SP, Stack grows downwards
            SP--;
        }

        // Pop a byte from the stack
        private byte Pop()
        {
            // Increase SP, Stack shrinks upwards
            SP++;

            // Read the value from the stack and return it
            return memoryBus.Read((ushort)(0x0100 + SP));
        }

        // Pop a 16-bit value from the stack
        private ushort Pop16()
        {
            // Pull two bytes in reverse order to reconstruct 16-bit value
            byte lo = Pop();
            byte hi = Pop();

            return (ushort)((hi << 8) | lo);
        }

        // Push a 16-bit value to the stack (Used for saving return addresses)
        private void Push16(ushort value)
        {
            // Hight byte first, the low byte (6502 Convention)
            Push((byte)((value >> 8) & 0xFF));  // Push high byte
            Push((byte)(value & 0xFF));         // Push low byte
        }

        // Flags Helper Methods
        private void SetFlag(byte mask) => Status |= mask;
        private void ClearFlag(byte mask) => Status &= (byte)~mask;
        private bool IsFlagSet(byte mask) => (Status & mask) != 0;

        // Set or clear status flag
        private void SetFlag(byte flag, bool value)
        {
            if (value)
            {
                Status |= flag;     // Set the flag bit
            }
            else
            {
                Status &= (byte)~flag; // Clear the flag bit
            }
        }

        // Get the value of a status flag
        private bool GetFlag(byte flag)
        {
            return (Status & flag) != 0;
        }

        // Registers
        public byte A;      // Accumulator
        public byte X;      // Index Register X
        public byte Y;      // Index Register Y
        public byte SP;     // Stack Pointer
        public ushort PC;   // Program Counter
        public byte Status; // Processor Status Register

        // Halt the stepping
        private bool Halt = false;

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
            if(Halt)
            {
                return;
            }

            // Fetch opcode at current PC
            byte opcode = Immediate();

            Logger.DebugLog($"[CPU] PC: 0x{PC:X4} Opcode: 0x{opcode:X2}");

            switch(opcode)
            {
                case 0xD8:  // CLD - Clear Decimal Mode
                    {
                        SetFlag(FLAG_DECIMAL, false);

                        Logger.DebugLog($"CLD - Decimal mode cleared");
                        break;
                    }
                case 0xA9:  // LDA Immediate
                    {
                        // Read the next byte as the immediate value
                        byte value = Immediate();

                        // Load the value into the accumulator
                        A = value;

                        // Update zero and negative flags based on the A value
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA #${value:X2} => Reg A = {A:X2}");
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
                        byte value = Immediate();

                        // Load X register with value
                        X = value;

                        // Update zero and negative flags based on the X value
                        SetZeroAndNegativeFlags(X);

                        Logger.DebugLog($"LDX #${value:X2} => Reg X = {X:X2}");
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
                        ushort addr = Absolute();

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
                case 0x00:  // BRK - Force Interrupt
                    {
                        // Increment PC to skip the next byte (dummy padding byte in BRK format)
                        PC++;

                        // Push PC and Status (with Break and Unused flags set)
                        Push16(PC);
                        Push((byte)(Status | FLAG_BREAK | FLAG_UNUSED));    // Set Break and Unused flags when pushing

                        // Set Interrupt Disable Flag
                        SetFlag(FLAG_INTERRUPT, true);


                        // Load PC from IRQ/BRK vector at 0xFFFE/0xFFFF
                        ushort lo = memoryBus.Read(0xFFFE);
                        ushort hi = memoryBus.Read(0xFFFF);
                        PC = (ushort)((hi << 8) | lo);

                        Logger.DebugLog($"BRK - Software Interrupt");
                        break;
                    }
                case 0x78:  // SEI Set Interrupt Disable
                    {
                        // Set bit 2  in the status register (Interrupt Disable Flag)
                        SetFlag(FLAG_INTERRUPT, true);

                        Logger.DebugLog("SEI - Interrupts Disabled");
                        break;
                    }
                case 0x69:  // ADC Immediate
                    {
                        // Add immediate value + carry flag to accumulator
                        byte value = Immediate();
                        int carry = (Status & 0x01) != 0 ? 1 : 0;
                        int sum = A + value + carry;

                        // Set CF if sum > 255
                        if(sum > 0xFF)
                        {
                            Status |= FLAG_CARRY;
                        }
                        else
                        {
                            Status &= 0xFE;
                        }

                        // Set overflow flag (signed)
                        bool overflow = (~(A ^ value) & (A ^ sum) & 0x80) != 0;

                        if(overflow)
                        {
                            Status |= FLAG_OVERFLOW;
                        }
                        else
                        {
                            Status &= 0xBF;
                        }

                        A = (byte)(sum & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ADC #${value:X2} => A = {A:X2}");
                        break;
                    }
                case 0x29:  // AND Immediate
                    {
                        // Bitwise and accumulator with immediate value
                        byte value = Immediate();
                        A = (byte)(A & value);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"AND #${value:X2} => A = {A:X2}");
                        break;
                    }
                case 0xC9:  // CMP Immediate
                    {
                        // Compare accumulator with immediate value
                        byte value = Immediate();
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

                        Logger.DebugLog($"CMP #${value:X2} => A is {A:X2}, Flags updated");
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
                        // Pull processor status from the stack
                        Status = Pop();

                        // Enforce unused bit to always be set
                        Status |= FLAG_UNUSED;

                        // Pull return address from stack
                        PC = Pop16();

                        Logger.DebugLog($"RTI - Return from Interrupt to PC (0x{PC:X4}");
                        break;
                    }
                case 0x48: // PHA - Push Accumulator
                    {
                        // Push A register onto the stack
                        Push(A);

                        Logger.DebugLog($"PHA - Pushed A ({A:X2}) to the stack");
                        break;
                    }
                case 0x68:  // PLA - Pull Accumulator
                    {
                        // Pull a value from the stack and store it in A
                        A = Pop();

                        Logger.DebugLog($"PLA - Pulled value ({A:X2}) from the stack into A register");
                        break;
                    }
                case 0x08: // PHP - Push Processor Status
                    {
                        // When pushing status to the stack, Bit 4 (Break) and Bit 5 (Unused) are typically set.
                        byte flagsToPush = (byte)(Status | FLAG_BREAK | FLAG_UNUSED);   // Set B (Bit 4) and U (Bit 5)
                        Push(flagsToPush);

                        Logger.DebugLog($"PHP - Pushed Status ({flagsToPush:X2}) to the stack");
                        break;
                    }
                case 0x28:  // PLP - Pull Processor Status
                    {
                        // Pull the status register from the stack
                        Status = Pop();

                        // Bit 5 (Unused) is always set on real 6502 CPU, enforce it manually
                        Status |= FLAG_UNUSED;

                        Logger.DebugLog($"PLP - Pulled Status ({Status:X2}) from the stack");
                        break;
                    }
                case 0x20:  // JSR - Jump to Subroutine (Absolute)
                    {
                        // Read 16-bit target address from next two bytes
                        ushort addr = (ushort)(Immediate() | (Immediate() << 8));

                        // Push return address - 1 (address of the last byte of the JSR instruction)
                        ushort returnAddr = (ushort)(PC - 1);
                        Push16(returnAddr);

                        // Jump to the target address
                        PC = addr;

                        Logger.DebugLog($"JSR $${addr:X4} - Return address 0x{returnAddr:X4} pushed");
                        break;
                    }
                case 0x60:  // RTS - Return from Subroutine
                    {
                        // Pop return address from the stack
                        ushort returnAddr = Pop16();

                        // Set PC to return address + 1
                        PC = (ushort)(returnAddr + 1);

                        Logger.DebugLog($"RTS - Return to 0x{PC:X4} (Popped 0x{returnAddr:X4})");
                        break;
                    }
                case 0xF8: // SED - Set Decimal Flag
                    {
                        SetFlag(FLAG_DECIMAL, true);

                        Logger.DebugLog($"SED - Decimal mode set");
                        break;
                    }
                case 0x18:  // CLC - Clear Carry Flag
                    {
                        ClearFlag(FLAG_CARRY);

                        Logger.DebugLog($"CLC - Carry Flag Cleared");
                        break;
                    }
                case 0x38:  // SEC - Set Carry Flag
                    {
                        SetFlag(FLAG_CARRY);

                        Logger.DebugLog($"SEC - Carry Flag Set");
                        break;
                    }
                case 0xAA:  // TAX - Transfer Accumulator to X
                    {
                        // Copy the value from Accumulator to X
                        X = A;

                        SetZeroAndNegativeFlags(X);

                        Logger.DebugLog($"TAX - Transfer A ({A:X2}) to X, X = ({X:X2})");
                        break;
                    }
                case 0x8A:  // TXA - Transfer X to Accumulator
                    {
                        // Copy the value from X register to Accumulator
                        A = X;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"TXA - Transfer X ({X:X2}) to A, A = ({A:X2})");
                        break;
                    }
                case 0xA8:  // TAY - Transfer Accumulator to Y
                    {
                        // Copy the value from the Accumulator to the Y register
                        Y = A;

                        SetZeroAndNegativeFlags(Y);

                        Logger.DebugLog($"TAY - Transfer A ({A:X2}) to Y, Y = ({Y:X2})");
                        break;
                    }
                case 0x98:  // TYA - Transfer Y to Accumulator
                    {
                        // Copy the value from the Y register into the Accumulator
                        A = Y;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"TYA - Transfer Y ({Y:X2}) to A, A = ({A:X2})");
                        break;
                    }
                case 0xCA:  // DEX - Decrement X Register
                    {
                        // Decrement the X register by 1
                        X--;

                        SetZeroAndNegativeFlags(X);

                        Logger.DebugLog($"DEX - Decrement X, X = {X:X2}");
                        break;
                    }
                case 0x88:  // DEY - Decrement Y Register
                    {
                        // Decrement the Y register by 1
                        Y--;

                        SetZeroAndNegativeFlags(Y);

                        Logger.DebugLog($"DEY - Decrement Y, Y = {Y:X2}");
                        break;
                    }
                case 0xA0:  // LDY Immediate
                    {
                        // Read the immediate byte 
                        byte value = Immediate();

                        // Load the value into the Y register
                        Y = value;

                        SetZeroAndNegativeFlags(Y);

                        Logger.DebugLog($"LDY #${value:X2}, Y = {Y:2}");
                        break;
                    }
                case 0x8E:  // STX Absolute
                    {
                        // Read the absolute address
                        ushort addr = Absolute();

                        // Store the value of the X register into memory at the given address
                        memoryBus.Write(addr, X);

                        Logger.DebugLog($"STX 0x{addr:X4} <= X ({X:X2})");
                        break;
                    }
                case 0x8C:  // STY Absolute
                    {
                        // Read the absolute address
                        ushort addr = Absolute();

                        // Store the value of the Y register into memory at the given address
                        memoryBus.Write(addr, Y);

                        Logger.DebugLog($"STY 0x{addr:X4} <= Y ({X:Y2})");
                        break;
                    }
                case 0x2C: // BIT Absolute
                    {
                        ushort addr = Absolute();
                        PC += 2;
                        byte value = Immediate();

                        SetFlag(FLAG_ZERO, (A & value) == 0);
                        SetFlag(FLAG_NEGATIVE, (value & FLAG_NEGATIVE) != 0);
                        SetFlag(FLAG_OVERFLOW, (value & FLAG_OVERFLOW) != 0);

                        Logger.DebugLog($"BIT ${addr:X4} = {value:X2}");
                        break;
                    }
                case 0x02: // KIL - Illegal Opcode
                    {
                        Logger.ErrorLog("Encountered illegal Opcode 0x02 (KIL) - CPU Halted!");
                        Halt = true;
                        break;
                    }
                default:
                    {
                        Logger.ErrorLog($"[CPU] Unknown Opcode: 0x{opcode:X2} at PC: {PC - 1:X4}");
                        break;
                    }
            }
        }

        private void SetZeroAndNegativeFlags(byte value)
        {
            SetFlag(FLAG_ZERO, value == 0);
            SetFlag(FLAG_NEGATIVE, (value & FLAG_NEGATIVE) != 0);
        }
    }
}
