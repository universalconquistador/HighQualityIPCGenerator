using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HQIPC;

internal struct IpcParameter
{
    public string Name { get; set; }
    public string FullyQualifiedTypeName { get; set; }
}

internal struct IpcInterfaceMethod
{
    public string Name { get; set; }
    public string NameCamelCase => char.ToLowerInvariant(Name[0]) + Name.Substring(1);

    public string FullyQualifiedReturnTypeName { get; set; }
    public IpcParameter[] Parameters { get; set; }
    public string FullyQualifiedDelegateName { get; set; }

    public string MakeGenericParameterString()
    {
        string[] types = new string[Parameters.Length + 1];

        for (int i = 0; i < Parameters.Length; i++)
        {
            types[i] = Parameters[i].FullyQualifiedTypeName;
        }
        types[Parameters.Length] = FullyQualifiedReturnTypeName == "void" ? "object" : FullyQualifiedReturnTypeName;

        return string.Join(", ", types);
    }

    public string MakeParameterDeclarationString()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Parameters.Length; i++)
        {
            var param = Parameters[i];
            sb.Append($"{param.FullyQualifiedTypeName} {param.Name}");
            if (i < Parameters.Length - 1)
            {
                sb.Append(", ");
            }
        }
        return sb.ToString();
    }

    public string MakeParameterString()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Parameters.Length; i++)
        {
            var param = Parameters[i];
            sb.Append(param.Name);
            if (i < Parameters.Length - 1)
            {
                sb.Append(", ");
            }
        }
        return sb.ToString();
    }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Name);
        hash.Add(FullyQualifiedReturnTypeName);
        hash.Add(Parameters.Length);
        foreach (var param in Parameters)
        {
            hash.Add(param);
        }
        return hash.ToHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is IpcInterfaceMethod method
            && Name == method.Name
            && FullyQualifiedReturnTypeName == method.FullyQualifiedReturnTypeName
            && Parameters.SequenceEqual(method.Parameters);
    }
}

internal readonly struct DiagnosticMessage
{
    public readonly DiagnosticDescriptor Descriptor { get; }
    public readonly string SourceFilename { get; }
    public readonly TextSpan Span { get; }
    public readonly LinePositionSpan LineSpan { get; }

    public DiagnosticMessage(DiagnosticDescriptor descriptor, string sourceFilename, TextSpan span, LinePositionSpan lineSpan)
    {
        Descriptor = descriptor;
        SourceFilename = sourceFilename;
        Span = span;
        LineSpan = lineSpan;
    }
}

internal struct IpcInterfaceJob
{
    public string Namespace { get; set; }
    public string InterfaceTypeName { get; set; }
    public string IpcNamespace { get; set; }

    public IpcInterfaceMethod[] Methods { get; set; }
    public IpcInterfaceMethod[] Events { get; set; }

    public DiagnosticMessage[] Messages { get; set; }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Namespace);
        hash.Add(InterfaceTypeName);
        hash.Add(Methods.Length);
        foreach (var method in Methods)
        {
            hash.Add(method);
        }
        hash.Add(Events.Length);
        foreach (var @event in Events)
        {
            hash.Add(@event);
        }
        hash.Add(Messages.Length);
        foreach (var message in Messages)
        {
            hash.Add(message);
        }
        return hash.ToHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is IpcInterfaceJob job
            && job.Namespace == Namespace
            && job.InterfaceTypeName == InterfaceTypeName
            && job.Methods.SequenceEqual(Methods)
            && job.Events.SequenceEqual(Events)
            && job.Messages.SequenceEqual(Messages);
    }
}
