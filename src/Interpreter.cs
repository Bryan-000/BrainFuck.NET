namespace BrainFuck.NET;

public unsafe struct Interpreter
{
    public void Execute(ReadOnlySpan<char> code)
    {
        byte* Pointer = stackalloc byte[30_000];

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
                    char input = Console.ReadKey().KeyChar;
                    if (input == '\r')
                        input = '\n';

                    *Pointer = (byte)input;
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
