namespace BrainFuck.NET;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

public struct Compiler
{
    public void CompileTo(string assemblyName, ReadOnlySpan<char> code)
    {
        MetadataBuilder metadata = new();
        BlobBuilder ilBuilder = new();

        var moduleType = CreateAsm(assemblyName, metadata);
        var entrypoint = CreateMembers(metadata, ilBuilder, code);

        using (FileStream fileStream = new(assemblyName + ".exe", FileMode.Create, FileAccess.ReadWrite))
        {
            PEHeaderBuilder peHeaderBuilder = new(imageCharacteristics: Characteristics.ExecutableImage);
            ManagedPEBuilder peBuilder = new
            (
                header: peHeaderBuilder,
                metadataRootBuilder: new MetadataRootBuilder(metadata),
                ilStream: ilBuilder,
                entryPoint: entrypoint,
                flags: CorFlags.ILOnly
            );

            BlobBuilder peBlob = new();

            BlobContentId contentId = peBuilder.Serialize(peBlob);
            peBlob.WriteContentTo(fileStream);
        }
    }

    TypeDefinitionHandle CreateAsm(string name, MetadataBuilder metadata)
    {
        metadata.AddAssembly
        (
            name: metadata.GetOrAddString(name),
            version: new(),
            culture: default,
            publicKey: default,
            flags: 0,
            hashAlgorithm: 0
        );

        metadata.AddModule
        (
            generation: 0,
            moduleName: metadata.GetOrAddString(name),
            mvid: metadata.GetOrAddGuid(Guid.NewGuid()),
            encId: default,
            encBaseId: default
        );

        return metadata.AddTypeDefinition
        (
            attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            @namespace: default,
            name: metadata.GetOrAddString(name),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1)
        );
    }

    MethodDefinitionHandle CreateMembers(MetadataBuilder metadata, BlobBuilder ilBuilder, ReadOnlySpan<char> code)
    {
        MethodBodyStreamEncoder methodBodyStream = new(ilBuilder);
        InstructionEncoder il = new(new(), new());

        GetReferences
        (
            out var writeRef,        out var writeStrRef,
            out var readKeyRef,      out var get_KeyCharRef,
            out var consoleKeyInfoRef
        );

        // add var signature for the Pointer variable
        BlobBuilder bytePtrSignature = new();
        new BlobEncoder(bytePtrSignature).
            FieldSignature().
                Pointer().Byte();


        // allocate the memory at the start of Main
        {
            // byte* Pointer = stackalloc byte[30_000];
            il.LoadConstantI4(30_000);
            il.OpCode(ILOpCode.Conv_u);
            il.OpCode(ILOpCode.Localloc);
            il.StoreLocal(0);
        }

        WriteBrainfuckAsCil(code);

        // wait for user input before exiting
        {
            // Console.WriteLine("Press any key to exit...")
            il.LoadString(metadata.GetOrAddUserString("\nPress any key to exit..."));
            il.Call(writeStrRef);

            // _ = Console.ReadKey(intercept: true)
            il.LoadConstantI4(1);
            il.Call(readKeyRef);
            il.OpCode(ILOpCode.Pop);

            // return
            il.OpCode(ILOpCode.Ret);
        }

        // create main method definition
        BlobBuilder mainSignature = new();
        new BlobEncoder(mainSignature).
            MethodSignature().
                Parameters
                (
                    parameterCount: 0,
                    returnType => returnType.Void(),
                    parameters => { }
                );

        BlobBuilder localVarsSignature = new();
        var localVarsEncoder = new BlobEncoder(localVarsSignature).LocalVariableSignature(2);

        // Pointer
        localVarsEncoder.AddVariable().Type().
            Pointer().Byte();

        // bullshit cuz of ','
        localVarsEncoder.AddVariable().Type().
                Type(consoleKeyInfoRef, true);

        return metadata.AddMethodDefinition
        (
            attributes: MethodAttributes.Public | MethodAttributes.Static,
            implAttributes: MethodImplAttributes.IL,
            name: metadata.GetOrAddString("Main"),
            signature: metadata.GetOrAddBlob(mainSignature),
            bodyOffset: methodBodyStream.AddMethodBody(il,
                localVariablesSignature: metadata.AddStandaloneSignature(
                    metadata.GetOrAddBlob(localVarsSignature)
                )
            ),
            parameterList: default
        );

        void GetReferences(out MemberReferenceHandle writeRef,        out MemberReferenceHandle writeStrRef,
                           out MemberReferenceHandle readKeyRef,      out MemberReferenceHandle get_KeyCharRef,
                           out TypeReferenceHandle consoleKeyInfoRef
        )
        {
            // get mscorlib
            var mscorlibRef = metadata.AddAssemblyReference
            (
                name: metadata.GetOrAddString("mscorlib"),
                version: new(4, 0, 0, 0),
                culture: default,
                publicKeyOrToken: metadata.GetOrAddBlob(new byte[] { 0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89 }),
                flags: default,
                hashValue: default
            );

            // get console
            var consoleRef = metadata.AddTypeReference
            (
                resolutionScope: mscorlibRef,
                @namespace: metadata.GetOrAddString("System"),
                name: metadata.GetOrAddString("Console")
            );

            // get ConsoleKeyInfo
            var consoleKeyInfoRefLoc = metadata.AddTypeReference
            (
                resolutionScope: mscorlibRef,
                @namespace: metadata.GetOrAddString("System"),
                name: metadata.GetOrAddString("ConsoleKeyInfo")
            );

            consoleKeyInfoRef = consoleKeyInfoRefLoc;

            // get Write(string)
            BlobBuilder writeStrSignature = new();
            new BlobEncoder(writeStrSignature).
                MethodSignature().
                    Parameters
                    (
                        parameterCount: 1,
                        returnType => returnType.Void(),
                        parameters => parameters.AddParameter().Type().String()
                    );

            writeStrRef = metadata.AddMemberReference
            (
                parent: consoleRef,
                name: metadata.GetOrAddString("Write"),
                signature: metadata.GetOrAddBlob(writeStrSignature)
            );

            // get Write(char)
            BlobBuilder writeSignature = new();
            new BlobEncoder(writeSignature).
                MethodSignature().
                    Parameters
                    (
                        parameterCount: 1,
                        returnType => returnType.Void(),
                        parameters => parameters.AddParameter().Type().Char()
                    );

            writeRef = metadata.AddMemberReference
            (
                parent: consoleRef,
                name: metadata.GetOrAddString("Write"),
                signature: metadata.GetOrAddBlob(writeSignature)
            );

            // get ReadKey
            BlobBuilder readKeySignature = new();
            new BlobEncoder(readKeySignature).
                MethodSignature().
                    Parameters
                    (
                        parameterCount: 1,
                        returnType => returnType.Type().Type(consoleKeyInfoRefLoc, true),
                        parameters => parameters.AddParameter().Type().Boolean()
                    );

            readKeyRef = metadata.AddMemberReference
            (
                parent: consoleRef,
                name: metadata.GetOrAddString("ReadKey"),
                signature: metadata.GetOrAddBlob(readKeySignature)
            );

            // get get_keychar
            BlobBuilder get_KeyCharSignature = new();
            new BlobEncoder(get_KeyCharSignature).
                MethodSignature(isInstanceMethod: true).
                    Parameters
                    (
                        parameterCount: 0,
                        returnType => returnType.Type().Char(),
                        parameters => { }
                    );

            get_KeyCharRef = metadata.AddMemberReference
            (
                parent: consoleKeyInfoRef,
                name: metadata.GetOrAddString("get_KeyChar"),
                signature: metadata.GetOrAddBlob(get_KeyCharSignature)
            );
        }

        void WriteBrainfuckAsCil(ReadOnlySpan<char> code)
        {
            LoopLabelPair[] labels;
            {
                int totalLoops = 0;
                foreach (char c in code)
                    if (c == '[')
                        totalLoops++;

                labels = new LoopLabelPair[totalLoops];
            }

            int currentLoop = 0;
            for (int i = 0; i < code.Length; i++)
            {
                int CountAndMove(ReadOnlySpan<char> code, char target)
                {
                    int ogI = i;
                    while (i+1 < code.Length && code[i+1] == target)
                        i++;

                    return (i - ogI) + 1;
                }

                switch (code[i])
                {
                    case '>':
                        // Pointer = Pointer + n;
                        il.LoadLocal(0);
                        il.LoadConstantI4(CountAndMove(code, '>'));
                        il.OpCode(ILOpCode.Add);

                        il.StoreLocal(0);
                    break;

                    case '<':
                        // Pointer = Pointer - n;
                        il.LoadLocal(0);
                        il.LoadConstantI4(CountAndMove(code, '<'));
                        il.OpCode(ILOpCode.Sub);

                        il.StoreLocal(0);
                    break;



                    case '+':
                        // *Pointer = *Pointer + n
                        il.LoadLocal(0);
                        il.OpCode(ILOpCode.Dup);

                        il.OpCode(ILOpCode.Ldind_u1);

                        il.LoadConstantI4(CountAndMove(code, '+'));
                        il.OpCode(ILOpCode.Add);

                        il.OpCode(ILOpCode.Conv_u1);
                        il.OpCode(ILOpCode.Stind_i1);
                    break;

                    case '-':
                        // *Pointer = *Pointer - n
                        il.LoadLocal(0);
                        il.OpCode(ILOpCode.Dup);

                        il.OpCode(ILOpCode.Ldind_u1);

                        il.LoadConstantI4(CountAndMove(code, '-'));
                        il.OpCode(ILOpCode.Sub);

                        il.OpCode(ILOpCode.Conv_u1);
                        il.OpCode(ILOpCode.Stind_i1);
                    break;



                    case '.':
                        // Console.Write((char)*Pointer)
                        il.LoadLocal(0);
                        il.OpCode(ILOpCode.Ldind_u1);

                        il.Call(writeRef);
                    break;

                    case ',':
                        // *Pointer = (byte)(Console.ReadKey().KeyChar if is not '\r' else '\n');
                        il.LoadLocal(0);

                        il.OpCode(ILOpCode.Ldc_i4_0);
                        il.Call(readKeyRef);

                        il.StoreLocal(1); // this is bullshit
                        il.LoadLocalAddress(1);

                        il.Call(get_KeyCharRef);

                        // if (char == '\r') char = '\n';
                        LabelHandle mraow = il.DefineLabel();
                        il.OpCode(ILOpCode.Dup);
                        il.LoadConstantI4(13); // \r
                        il.Branch(ILOpCode.Bne_un_s, mraow);

                        il.OpCode(ILOpCode.Pop);
                        il.LoadConstantI4(10); // \n

                        il.MarkLabel(mraow);
                        il.OpCode(ILOpCode.Conv_u1);
                        il.OpCode(ILOpCode.Stind_i1);
                    break;



                    case '[':
                        // this handles initial comment loop's, by just skipping them lmao
                        if (i == 0)
                        {
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
                        }

                        // while (*Pointer != 0) {
                        LoopLabelPair pair = new(il);
                        labels[currentLoop++] = pair;

                        il.MarkLabel(pair.Left);

                        il.LoadLocal(0);
                        il.OpCode(ILOpCode.Ldind_u1);
                        il.OpCode(ILOpCode.Ldc_i4_0);

                        il.OpCode(ILOpCode.Ceq);
                        il.Branch(ILOpCode.Brtrue, pair.Right);
                    break;

                    case ']':
                        // }
                        pair = labels[--currentLoop];
                        il.Branch(ILOpCode.Br, pair.Left);

                        il.MarkLabel(pair.Right);
                    break;
                }
            }
        }
    }

    struct LoopLabelPair(InstructionEncoder il)
    {
        public LabelHandle Left  = il.DefineLabel();
        public LabelHandle Right = il.DefineLabel();
    }
}