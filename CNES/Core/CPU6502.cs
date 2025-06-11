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

                        Logger.DebugLog($"STY 0x{addr:X4} <= Y ({Y:X2})");
                        break;
                    }
                case 0x24:  // BIT Zero Page
                    {
                        // Read zero page address and value
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);

                        // Set zero flag if (A & value) == 0
                        SetFlag(FLAG_ZERO, (A & value) == 0);

                        // Set negative flag to bit 7 of value
                        SetFlag(FLAG_NEGATIVE, (value & FLAG_NEGATIVE) != 0);

                        // Set overflow flag to bit 6 of value
                        SetFlag(FLAG_OVERFLOW, (value & FLAG_OVERFLOW) != 0);

                        Logger.DebugLog($"BIT ${addr:X2} = {value:X2}");
                        break;
                    }
                case 0x2C: // BIT Absolute
                    {
                        // Read absolute address and value
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);

                        // Set zero flag if (A & value) == 0
                        SetFlag(FLAG_ZERO, (A & value) == 0);

                        // Set negative flag to bit 7 of value
                        SetFlag(FLAG_NEGATIVE, (value & FLAG_NEGATIVE) != 0);

                        // Set overflow flag to bit 6 of value
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
                case 0xA5:  // LDA Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA ${addr:X2} => Reg A = {A:X2}");
                        break;
                    }
                case 0xB5:  // LDA Zero Page,X
                    {
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA ${addr:X2},X => Reg A = {A:X2}");
                        break;
                    }
                case 0xAD:  // LDA Absolute
                    {
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA ${addr:X4} => Reg A = {A:X2}");
                        break;
                    }
                case 0xBD:  // LDA Absolute,X
                    {
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA ${baseAddr:X4},X => Reg A = {A:X2}");
                        break;
                    }
                case 0xB9:  // LDA Absolute,Y
                    {
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA ${baseAddr:X4},Y => Reg A = {A:X2}");
                        break;
                    }
                case 0xA1:  // LDA (Indirect,X)
                    {
                        byte zpAddr = (byte)(memoryBus.Read(PC++) + X);
                        ushort addr = (ushort)(memoryBus.Read((byte)zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA (${zpAddr:X2},X) => Reg A = {A:X2}");
                        break;
                    }
                case 0xB1:  // LDA (Indirect),Y
                    {
                        byte zpAddr = memoryBus.Read(PC++);
                        ushort baseAddr = (ushort)(memoryBus.Read(zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        A = value;

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"LDA (${zpAddr:X2}),Y => Reg A = {A:X2}");
                        break;
                    }
                case 0xE9:  // SBC Immediate
                    {
                        // Read the immediate value
                        byte value = Immediate();

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC #${value:X2} => A = {A:X2}");
                        break;
                    }
                case 0xE5:  // SBC Zero Page
                    {
                        // Read zero page address and value
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);

                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        SetFlag(FLAG_CARRY, result >= 0);

                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC ${addr:X2} => A = {A:X2}");
                        break;
                    }
                case 0xF5:  // SBC Zero Page,X
                    {
                        // Read zero page address, add X register for indexed addressing
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC ${addr:X2},X => A = {A:X2}");
                        break;
                    }
                case 0xED:  // SBC Absolute
                    {
                        // Read absolute address (16-bit)
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC ${addr:X4} => A = {A:X2}");
                        break;
                    }
                case 0xF9:  // SBC Absolute,Y
                    {
                        // Read absolute address and add Y register for indexed addressing
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC ${baseAddr:X4},Y => A = {A:X2}");
                        break;
                    }
                case 0xFD:  // SBC Absolute,X
                    {
                        // Read absolute address and add X register for indexed addressing
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC ${baseAddr:X4},X => A = {A:X2}");
                        break;
                    }
                case 0xE1:  // SBC (Indirect,X)
                    {
                        // Read zero page address, add X for indexed indirect addressing
                        byte zpAddr = (byte)(memoryBus.Read(PC++) + X);
                        ushort addr = (ushort)(memoryBus.Read((byte)zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        byte value = memoryBus.Read(addr);

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC (${zpAddr:X2},X) => A = {A:X2}");
                        break;
                    }
                case 0xF1:  // SBC (Indirect),Y
                    {
                        // Read zero page address, fetch pointer, add Y for indirect indexed addressing
                        byte zpAddr = memoryBus.Read(PC++);
                        ushort baseAddr = (ushort)(memoryBus.Read(zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        // Subtract value and carry from accumulator
                        int carry = (Status & FLAG_CARRY) != 0 ? 0 : 1;
                        int result = A - value - carry;

                        // Set carry if result >= 0
                        SetFlag(FLAG_CARRY, result >= 0);

                        // Set overflow flag
                        bool overflow = ((A ^ value) & (A ^ result) & 0x80) != 0;
                        SetFlag(FLAG_OVERFLOW, overflow);

                        A = (byte)(result & 0xFF);

                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"SBC (${zpAddr:X2}),Y => A = {A:X2}");
                        break;
                    }
                case 0x09:  // ORA Immediate
                    {
                        // Read the immediate value
                        byte value = Immediate();

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA #${value:X2} => A = {A:X2}");
                        break;
                    }
                case 0x05:  // ORA Zero Page
                    {
                        // Read zero page address and value
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA ${addr:X2} => A = {A:X2}");
                        break;
                    }
                case 0x15:  // ORA Zero Page,X
                    {
                        // Read zero page address, add X register for indexed addressing
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA ${addr:X2},X => A = {A:X2}");
                        break;
                    }
                case 0x0D:  // ORA Absolute
                    {
                        // Read absolute address (16-bit)
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA ${addr:X4} => A = {A:X2}");
                        break;
                    }
                case 0x1D:  // ORA Absolute,X
                    {
                        // Read absolute address and add X register for indexed addressing
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA ${baseAddr:X4},X => A = {A:X2}");
                        break;
                    }
                case 0x19:  // ORA Absolute,Y
                    {
                        // Read absolute address and add Y register for indexed addressing
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA ${baseAddr:X4},Y => A = {A:X2}");
                        break;
                    }
                case 0x01:  // ORA (Indirect,X)
                    {
                        // Read zero page address, add X for indexed indirect addressing
                        byte zpAddr = (byte)(memoryBus.Read(PC++) + X);
                        ushort addr = (ushort)(memoryBus.Read((byte)zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA (${zpAddr:X2},X) => A = {A:X2}");
                        break;
                    }
                case 0x11:  // ORA (Indirect),Y
                    {
                        // Read zero page address, fetch pointer, add Y for indirect indexed addressing
                        byte zpAddr = memoryBus.Read(PC++);
                        ushort baseAddr = (ushort)(memoryBus.Read(zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        // Bitwise OR with accumulator
                        A = (byte)(A | value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"ORA (${zpAddr:X2}),Y => A = {A:X2}");
                        break;
                    }
                case 0x49:  // EOR Immediate
                    {
                        // Read the immediate value
                        byte value = Immediate();

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR #${value:X2} => A = {A:X2}");
                        break;
                    }
                case 0x45:  // EOR Zero Page
                    {
                        // Read zero page address and value
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR ${addr:X2} => A = {A:X2}");
                        break;
                    }
                case 0x55:  // EOR Zero Page,X
                    {
                        // Read zero page address, add X register for indexed addressing
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR ${addr:X2},X => A = {A:X2}");
                        break;
                    }
                case 0x4D:  // EOR Absolute
                    {
                        // Read absolute address (16-bit)
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR ${addr:X4} => A = {A:X2}");
                        break;
                    }
                case 0x5D:  // EOR Absolute,X
                    {
                        // Read absolute address and add X register for indexed addressing
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR ${baseAddr:X4},X => A = {A:X2}");
                        break;
                    }
                case 0x59:  // EOR Absolute,Y
                    {
                        // Read absolute address and add Y register for indexed addressing
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR ${baseAddr:X4},Y => A = {A:X2}");
                        break;
                    }
                case 0x41:  // EOR (Indirect,X)
                    {
                        // Read zero page address, add X for indexed indirect addressing
                        byte zpAddr = (byte)(memoryBus.Read(PC++) + X);
                        ushort addr = (ushort)(memoryBus.Read((byte)zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR (${zpAddr:X2},X) => A = {A:X2}");
                        break;
                    }
                case 0x51:  // EOR (Indirect),Y
                    {
                        // Read zero page address, fetch pointer, add Y for indirect indexed addressing
                        byte zpAddr = memoryBus.Read(PC++);
                        ushort baseAddr = (ushort)(memoryBus.Read(zpAddr) | (memoryBus.Read((byte)(zpAddr + 1)) << 8));
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);

                        // Bitwise exclusive OR with accumulator
                        A = (byte)(A ^ value);

                        // Update zero and negative flags based on the result
                        SetZeroAndNegativeFlags(A);

                        Logger.DebugLog($"EOR (${zpAddr:X2}),Y => A = {A:X2}");
                        break;
                    }
                case 0x9A:  // TXS - Transfer X to Stack Pointer
                    {
                        // Copy the value from the X register to the stack pointer
                        SP = X;

                        Logger.DebugLog($"TXS - Transfer X ({X:X2}) to SP, SP = ({SP:X2})");
                        break;
                    }
                case 0xBA:  // TSX - Transfer Stack Pointer to X
                    {
                        // Copy the value from the stack pointer to the X register
                        X = SP;

                        // Update zero and negative flags based on the X value
                        SetZeroAndNegativeFlags(X);

                        Logger.DebugLog($"TSX - Transfer SP ({SP:X2}) to X, X = ({X:X2})");
                        break;
                    }
                case 0x90:  // BCC - Branch if Carry Clear
                    {
                        // Read relative offset as signed byte
                        sbyte offset = (sbyte)Immediate();
                        if (!GetFlag(FLAG_CARRY))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BCC to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BCC not taken");
                        }
                        break;
                    }
                case 0xB0:  // BCS - Branch if Carry Set
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (GetFlag(FLAG_CARRY))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BCS to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BCS not taken");
                        }
                        break;
                    }
                case 0xF0:  // BEQ - Branch if Equal (Zero set)
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (GetFlag(FLAG_ZERO))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BEQ to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BEQ not taken");
                        }
                        break;
                    }
                case 0x30:  // BMI - Branch if Minus (Negative set)
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (GetFlag(FLAG_NEGATIVE))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BMI to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BMI not taken");
                        }
                        break;
                    }
                case 0xD0:  // BNE - Branch if Not Equal (Zero clear)
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (!GetFlag(FLAG_ZERO))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BNE to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BNE not taken");
                        }
                        break;
                    }
                case 0x10:  // BPL - Branch if Plus (Negative clear)
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (!GetFlag(FLAG_NEGATIVE))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BPL to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BPL not taken");
                        }
                        break;
                    }
                case 0x50:  // BVC - Branch if Overflow Clear
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (!GetFlag(FLAG_OVERFLOW))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BVC to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BVC not taken");
                        }
                        break;
                    }
                case 0x70:  // BVS - Branch if Overflow Set
                    {
                        sbyte offset = (sbyte)Immediate();
                        if (GetFlag(FLAG_OVERFLOW))
                        {
                            ushort oldPC = PC;
                            PC = (ushort)(PC + offset);
                            Logger.DebugLog($"BVS to 0x{PC:X4} (offset {offset}) from 0x{oldPC:X4}");
                        }
                        else
                        {
                            Logger.DebugLog("BVS not taken");
                        }
                        break;
                    }
                case 0x0A:  // ASL Accumulator
                    {
                        // Shift accumulator left by 1, bit 7 to carry
                        SetFlag(FLAG_CARRY, (A & 0x80) != 0);
                        A = (byte)(A << 1);
                        SetZeroAndNegativeFlags(A);
                        Logger.DebugLog($"ASL A => A = {A:X2}");
                        break;
                    }
                case 0x06:  // ASL Zero Page
                    {
                        // Shift memory at zero page address left by 1
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)(value << 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ASL ${addr:X2} => {value:X2}");
                        break;
                    }
                case 0x16:  // ASL Zero Page,X
                    {
                        // Shift memory at zero page address + X left by 1
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)(value << 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ASL ${addr:X2},X => {value:X2}");
                        break;
                    }
                case 0x0E:  // ASL Absolute
                    {
                        // Shift memory at absolute address left by 1
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)(value << 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ASL ${addr:X4} => {value:X2}");
                        break;
                    }
                case 0x1E:  // ASL Absolute,X
                    {
                        // Shift memory at absolute address + X left by 1
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)(value << 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ASL ${baseAddr:X4},X => {value:X2}");
                        break;
                    }
                case 0x4A:  // LSR Accumulator
                    {
                        // Shift accumulator right by 1, bit 0 to carry
                        SetFlag(FLAG_CARRY, (A & 0x01) != 0);
                        A = (byte)(A >> 1);
                        SetZeroAndNegativeFlags(A);
                        Logger.DebugLog($"LSR A => A = {A:X2}");
                        break;
                    }
                case 0x46:  // LSR Zero Page
                    {
                        // Shift memory at zero page address right by 1
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)(value >> 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"LSR ${addr:X2} => {value:X2}");
                        break;
                    }
                case 0x56:  // LSR Zero Page,X
                    {
                        // Shift memory at zero page address + X right by 1
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)(value >> 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"LSR ${addr:X2},X => {value:X2}");
                        break;
                    }
                case 0x4E:  // LSR Absolute
                    {
                        // Shift memory at absolute address right by 1
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)(value >> 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"LSR ${addr:X4} => {value:X2}");
                        break;
                    }
                case 0x5E:  // LSR Absolute,X
                    {
                        // Shift memory at absolute address + X right by 1
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)(value >> 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"LSR ${baseAddr:X4},X => {value:X2}");
                        break;
                    }
                case 0x2A:  // ROL Accumulator
                    {
                        // Rotate accumulator left through carry
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (A & 0x80) != 0);
                        A = (byte)((A << 1) | (oldCarry ? 1 : 0));
                        SetZeroAndNegativeFlags(A);
                        Logger.DebugLog($"ROL A => A = {A:X2}");
                        break;
                    }
                case 0x26:  // ROL Zero Page
                    {
                        // Rotate memory at zero page address left through carry
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)((value << 1) | (oldCarry ? 1 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROL ${addr:X2} => {value:X2}");
                        break;
                    }
                case 0x36:  // ROL Zero Page,X
                    {
                        // Rotate memory at zero page address + X left through carry
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)((value << 1) | (oldCarry ? 1 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROL ${addr:X2},X => {value:X2}");
                        break;
                    }
                case 0x2E:  // ROL Absolute
                    {
                        // Rotate memory at absolute address left through carry
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)((value << 1) | (oldCarry ? 1 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROL ${addr:X4} => {value:X2}");
                        break;
                    }
                case 0x3E:  // ROL Absolute,X
                    {
                        // Rotate memory at absolute address + X left through carry
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x80) != 0);
                        value = (byte)((value << 1) | (oldCarry ? 1 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROL ${baseAddr:X4},X => {value:X2}");
                        break;
                    }
                case 0x6A:  // ROR Accumulator
                    {
                        // Rotate accumulator right through carry
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (A & 0x01) != 0);
                        A = (byte)((A >> 1) | (oldCarry ? 0x80 : 0));
                        SetZeroAndNegativeFlags(A);
                        Logger.DebugLog($"ROR A => A = {A:X2}");
                        break;
                    }
                case 0x66:  // ROR Zero Page
                    {
                        // Rotate memory at zero page address right through carry
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROR ${addr:X2} => {value:X2}");
                        break;
                    }
                case 0x76:  // ROR Zero Page,X
                    {
                        // Rotate memory at zero page address + X right through carry
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROR ${addr:X2},X => {value:X2}");
                        break;
                    }
                case 0x6E:  // ROR Absolute
                    {
                        // Rotate memory at absolute address right through carry
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROR ${addr:X4} => {value:X2}");
                        break;
                    }
                case 0x7E:  // ROR Absolute,X
                    {
                        // Rotate memory at absolute address + X right through carry
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);
                        bool oldCarry = GetFlag(FLAG_CARRY);
                        SetFlag(FLAG_CARRY, (value & 0x01) != 0);
                        value = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"ROR ${baseAddr:X4},X => {value:X2}");
                        break;
                    }
                case 0xE6:  // INC Zero Page
                    {
                        // Increment memory at zero page address by 1
                        ushort addr = memoryBus.Read(PC++);
                        byte value = (byte)(memoryBus.Read(addr) + 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"INC ${addr:X2} => {value:X2}");
                        break;
                    }
                case 0xF6:  // INC Zero Page,X
                    {
                        // Increment memory at zero page address + X by 1
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = (byte)(memoryBus.Read(addr) + 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"INC ${addr:X2},X => {value:X2}");
                        break;
                    }
                case 0xEE:  // INC Absolute
                    {
                        // Increment memory at absolute address by 1
                        ushort addr = Absolute();
                        byte value = (byte)(memoryBus.Read(addr) + 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"INC ${addr:X4} => {value:X2}");
                        break;
                    }
                case 0xFE:  // INC Absolute,X
                    {
                        // Increment memory at absolute address + X by 1
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = (byte)(memoryBus.Read(addr) + 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"INC ${baseAddr:X4},X => {value:X2}");
                        break;
                    }
                case 0xC6:  // DEC Zero Page
                    {
                        // Decrement memory at zero page address by 1
                        ushort addr = memoryBus.Read(PC++);
                        byte value = (byte)(memoryBus.Read(addr) - 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"DEC ${addr:X2} => {value:X2}");
                        break;
                    }
                case 0xD6:  // DEC Zero Page,X
                    {
                        // Decrement memory at zero page address + X by 1
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = (byte)(memoryBus.Read(addr) - 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"DEC ${addr:X2},X => {value:X2}");
                        break;
                    }
                case 0xCE:  // DEC Absolute
                    {
                        // Decrement memory at absolute address by 1
                        ushort addr = Absolute();
                        byte value = (byte)(memoryBus.Read(addr) - 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"DEC ${addr:X4} => {value:X2}");
                        break;
                    }
                case 0xDE:  // DEC Absolute,X
                    {
                        // Decrement memory at absolute address + X by 1
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = (byte)(memoryBus.Read(addr) - 1);
                        memoryBus.Write(addr, value);
                        SetZeroAndNegativeFlags(value);
                        Logger.DebugLog($"DEC ${baseAddr:X4},X => {value:X2}");
                        break;
                    }
                case 0xE0:  // CPX Immediate
                    {
                        byte value = Immediate();
                        int result = X - value;
                        SetFlag(FLAG_CARRY, X >= value);
                        SetZeroAndNegativeFlags((byte)result);
                        Logger.DebugLog($"CPX #${value:X2} => X = {X:X2}, Flags updated");
                        break;
                    }
                case 0xE4:  // CPX Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        int result = X - value;
                        SetFlag(FLAG_CARRY, X >= value);
                        SetZeroAndNegativeFlags((byte)result);
                        Logger.DebugLog($"CPX ${addr:X2} => X = {X:X2}, Flags updated");
                        break;
                    }
                case 0xEC:  // CPX Absolute
                    {
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        int result = X - value;
                        SetFlag(FLAG_CARRY, X >= value);
                        SetZeroAndNegativeFlags((byte)result);
                        Logger.DebugLog($"CPX ${addr:X4} => X = {X:X2}, Flags updated");
                        break;
                    }
                case 0xC0:  // CPY Immediate
                    {
                        byte value = Immediate();
                        int result = Y - value;
                        SetFlag(FLAG_CARRY, Y >= value);
                        SetZeroAndNegativeFlags((byte)result);
                        Logger.DebugLog($"CPY #${value:X2} => Y = {Y:X2}, Flags updated");
                        break;
                    }
                case 0xC4:  // CPY Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        int result = Y - value;
                        SetFlag(FLAG_CARRY, Y >= value);
                        SetZeroAndNegativeFlags((byte)result);
                        Logger.DebugLog($"CPY ${addr:X2} => Y = {Y:X2}, Flags updated");
                        break;
                    }
                case 0xCC:  // CPY Absolute
                    {
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        int result = Y - value;
                        SetFlag(FLAG_CARRY, Y >= value);
                        SetZeroAndNegativeFlags((byte)result);
                        Logger.DebugLog($"CPY ${addr:X4} => Y = {Y:X2}, Flags updated");
                        break;
                    }
                case 0xA6:  // LDX Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        X = value;
                        SetZeroAndNegativeFlags(X);
                        Logger.DebugLog($"LDX ${addr:X2} => X = {X:X2}");
                        break;
                    }
                case 0xB6:  // LDX Zero Page,Y
                    {
                        ushort addr = (byte)(memoryBus.Read(PC++) + Y);
                        byte value = memoryBus.Read(addr);
                        X = value;
                        SetZeroAndNegativeFlags(X);
                        Logger.DebugLog($"LDX ${addr:X2},Y => X = {X:X2}");
                        break;
                    }
                case 0xAE:  // LDX Absolute
                    {
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        X = value;
                        SetZeroAndNegativeFlags(X);
                        Logger.DebugLog($"LDX ${addr:X4} => X = {X:X2}");
                        break;
                    }
                case 0xBE:  // LDX Absolute,Y
                    {
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + Y);
                        byte value = memoryBus.Read(addr);
                        X = value;
                        SetZeroAndNegativeFlags(X);
                        Logger.DebugLog($"LDX ${baseAddr:X4},Y => X = {X:X2}");
                        break;
                    }
                case 0xA4:  // LDY Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        byte value = memoryBus.Read(addr);
                        Y = value;
                        SetZeroAndNegativeFlags(Y);
                        Logger.DebugLog($"LDY ${addr:X2} => Y = {Y:X2}");
                        break;
                    }
                case 0xB4:  // LDY Zero Page,X
                    {
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        byte value = memoryBus.Read(addr);
                        Y = value;
                        SetZeroAndNegativeFlags(Y);
                        Logger.DebugLog($"LDY ${addr:X2},X => Y = {Y:X2}");
                        break;
                    }
                case 0xAC:  // LDY Absolute
                    {
                        ushort addr = Absolute();
                        byte value = memoryBus.Read(addr);
                        Y = value;
                        SetZeroAndNegativeFlags(Y);
                        Logger.DebugLog($"LDY ${addr:X4} => Y = {Y:X2}");
                        break;
                    }
                case 0xBC:  // LDY Absolute,X
                    {
                        ushort baseAddr = Absolute();
                        ushort addr = (ushort)(baseAddr + X);
                        byte value = memoryBus.Read(addr);
                        Y = value;
                        SetZeroAndNegativeFlags(Y);
                        Logger.DebugLog($"LDY ${baseAddr:X4},X => Y = {Y:X2}");
                        break;
                    }
                case 0x86:  // STX Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        memoryBus.Write(addr, X);
                        Logger.DebugLog($"STX ${addr:X2} <= X ({X:X2})");
                        break;
                    }
                case 0x96:  // STX Zero Page,Y
                    {
                        ushort addr = (byte)(memoryBus.Read(PC++) + Y);
                        memoryBus.Write(addr, X);
                        Logger.DebugLog($"STX ${addr:X2},Y <= X ({X:X2})");
                        break;
                    }
                case 0x84:  // STY Zero Page
                    {
                        ushort addr = memoryBus.Read(PC++);
                        memoryBus.Write(addr, Y);
                        Logger.DebugLog($"STY ${addr:X2} <= Y ({Y:X2})");
                        break;
                    }
                case 0x94:  // STY Zero Page,X
                    {
                        ushort addr = (byte)(memoryBus.Read(PC++) + X);
                        memoryBus.Write(addr, Y);
                        Logger.DebugLog($"STY ${addr:X2},X <= Y ({Y:X2})");
                        break;
                    }
                case 0x6C:  // JMP (Indirect)
                    {
                        // Fetch pointer address (little-endian)
                        ushort ptr = Absolute();

                        // Emulate 6502 page boundary hardware bug:
                        // If the pointer address crosses a page (e.g., $xxFF), the high byte is fetched from $xx00
                        byte lo = memoryBus.Read(ptr);
                        byte hi;
                        if ((ptr & 0x00FF) == 0x00FF)
                        {
                            // Simulate page boundary bug
                            hi = memoryBus.Read((ushort)(ptr & 0xFF00));
                        }
                        else
                        {
                            hi = memoryBus.Read((ushort)(ptr + 1));
                        }
                        ushort addr = (ushort)((hi << 8) | lo);

                        PC = addr;

                        Logger.DebugLog($"JMP (${ptr:X4}) => {addr:X4}");
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
