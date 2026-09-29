namespace BrainFuck.NET;

using System.Runtime.InteropServices;

public unsafe struct Interpreter : IDisposable
{
    public Interpreter()
    {
        Memory = (byte*)NativeMemory.AllocZeroed(30_000);
        Pointer = Memory;
    }

    public void Dispose()
    {
        NativeMemory.Free(Memory);
        Pointer = Memory = null;
    }

    byte* Memory;
    byte* Pointer;

    public void Execute(ReadOnlySpan<char> code)
    {
        for (int i = 0; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '>':
                    Pointer++;
                break;

                case '<':
                    Pointer--;
                break;


                case '+':
                    (*Pointer)++;
                break;

                case '-':
                    (*Pointer)--;
                break;


                case '.':
                    Console.Write((char)*Pointer);
                break;

                case ',':
                    *Pointer = (byte)Console.ReadKey().KeyChar;
                break;


                case '[' when *Pointer == 0:
                    int otherPairs = 0, ogI = i;
                    while (true)
                    {
                        if (i++ > code.Length)
                            throw new IndexOutOfRangeException($"Opening '[' at index {ogI} doesn't have a matching partner.");

                        char current = code[i];
                        if (current == '[')
                        {
                            otherPairs++;
                        }
                        else if (current == ']')
                        {
                            if (otherPairs == 0)
                                break;

                            otherPairs--;
                        }
                    }
                break;

                case ']' when *Pointer != 0:
                    otherPairs = 0; ogI = i;
                    while (true)
                    {
                        if (i-- < 0)
                            throw new IndexOutOfRangeException($"Closing ']' at index {ogI} doesn't have a matching partner.");

                        char current = code[i];
                        if (current == ']')
                        {
                            otherPairs++;
                        }
                        else if (current == '[')
                        {
                            if (otherPairs == 0)
                                break;

                            otherPairs--;
                        }
                    }
                break;
            }
        }
    }
}
