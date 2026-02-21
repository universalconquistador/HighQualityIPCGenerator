

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace HQIPC;

[Generator]
public class Generator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor _nonVoidEventReturnRule = new DiagnosticDescriptor("HQIPC01", "Non-void IPC event", "Dalamud events cannot have a return type", "IPC", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(GenerateSharedSource);

        var ipcInterfaceJobs = context.SyntaxProvider.ForAttributeWithMetadataName("HQIPC.IpcInterfaceAttribute", (node, _) => node is InterfaceDeclarationSyntax, GenerateIpcInterfaceJob);

        context.RegisterSourceOutput(ipcInterfaceJobs, GenerateIpcInterfaceSource);
    }

    void GenerateSharedSource(IncrementalGeneratorPostInitializationContext context)
    {
        // Add the [IpcInterface(string ipcNamespace)] attribute
        context.AddSource($"Shared", "namespace HQIPC \n{ \n    [System.AttributeUsage(System.AttributeTargets.Interface)] \n    public class IpcInterfaceAttribute : System.Attribute \n    { \n        public string IpcNamespace { get; set; } \n        public IpcInterfaceAttribute(string ipcNamespace) \n        { \n            IpcNamespace = ipcNamespace; \n        } \n    } \n}");
    }

    // Extract everything we need from the syntax & semantics so that we can generate the source files without them
    IpcInterfaceJob GenerateIpcInterfaceJob(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        var result = new IpcInterfaceJob();
        result.IpcNamespace = context.Attributes[0].ConstructorArguments[0].Value as string ?? "";
        var declaration = context.TargetSymbol;
        if (declaration is ITypeSymbol typeSymbol)
        {
            result.Namespace = typeSymbol.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
            result.InterfaceTypeName = typeSymbol.Name;
            List<IpcInterfaceMethod> methods = new List<IpcInterfaceMethod>();
            List<IpcInterfaceMethod> events = new List<IpcInterfaceMethod>();
            List<DiagnosticMessage> messages = new List<DiagnosticMessage>();
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Ordinary)
                {
                    List<IpcParameter> parameters = new List<IpcParameter>();
                    foreach (var param in methodSymbol.Parameters)
                    {
                        parameters.Add(new IpcParameter { FullyQualifiedTypeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), Name = param.Name });
                    }

                    methods.Add(new IpcInterfaceMethod()
                    {
                        Name = methodSymbol.Name,
                        FullyQualifiedReturnTypeName = methodSymbol.ReturnType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty,
                        Parameters = parameters.ToArray(),
                    });
                }
                else if (member is IEventSymbol eventSymbol && eventSymbol.Type.TypeKind == TypeKind.Delegate)
                {
                    List<IpcParameter> parameters = new List<IpcParameter>();

                    // We can't directly query the delegate type for its signature, but we *can* query its `Invoke` method's signature which is always identical
                    var invokeSymbol = eventSymbol.Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol;

                    // DOES NOT WORK, I'm not entirely clear why
                    //var invokeSymbol = eventSymbol.RaiseMethod;

                    if (invokeSymbol != null)
                    {
                        if (!invokeSymbol.ReturnsVoid)
                        {
                            messages.Add(new DiagnosticMessage(_nonVoidEventReturnRule, eventSymbol.DeclaringSyntaxReferences[0].GetSyntax().GetLocation().GetLineSpan().Path, eventSymbol.DeclaringSyntaxReferences[0].GetSyntax().GetLocation().SourceSpan, eventSymbol.DeclaringSyntaxReferences[0].GetSyntax().GetLocation().GetLineSpan().Span));
                            continue;
                        }

                        foreach (var param in invokeSymbol.Parameters)
                        {
                            parameters.Add(new IpcParameter { FullyQualifiedTypeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), Name = param.Name });
                        }

                        events.Add(new IpcInterfaceMethod()
                        {
                            Name = eventSymbol.Name,
                            FullyQualifiedReturnTypeName = invokeSymbol.ReturnType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty,
                            Parameters = parameters.ToArray(),
                            FullyQualifiedDelegateName = eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        });
                    }

                }
            }
            result.Methods = methods.ToArray();
            result.Events = events.ToArray();
            result.Messages = messages.ToArray();
        }
        return result;
    }

    void GenerateIpcInterfaceSource(SourceProductionContext context, IpcInterfaceJob job)
    {
        IndentingStringBuilder sb = new IndentingStringBuilder();

        var nonInterfaceName = job.InterfaceTypeName;
        if (nonInterfaceName.Length > 1 && nonInterfaceName[0] == 'I')
        {
            nonInterfaceName = nonInterfaceName.Substring(1);
        }
        
        sb.AppendLine($"// Automatically generated by {typeof(Generator).Assembly.FullName}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {job.Namespace.Substring("global::".Length)}");
        using (sb.BeginBraceScope())
        {
            sb.AppendLine($"public interface {job.InterfaceTypeName}Consumer : {job.InterfaceTypeName}, System.IDisposable");
            sb.AppendLine("{ }");
            sb.AppendLine();
            sb.AppendLine($"public static class {nonInterfaceName}");
            using (sb.BeginBraceScope())
            {
                sb.AppendLine("private class Provider : System.IDisposable");
                using (sb.BeginBraceScope())
                {
                    sb.AppendLine($"private readonly {job.InterfaceTypeName} _implementation;");
                    sb.AppendLine($"private readonly System.Collections.Generic.List<Dalamud.Plugin.Ipc.ICallGateProvider> _providers = new System.Collections.Generic.List<Dalamud.Plugin.Ipc.ICallGateProvider>();");
                    
                    // Event providers
                    sb.AppendLine();
                    foreach (var eventInfo in job.Events)
                    {
                        sb.AppendLine($"private Dalamud.Plugin.Ipc.ICallGateProvider<{eventInfo.MakeGenericParameterString()}> _{eventInfo.NameCamelCase};");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"public Provider({job.InterfaceTypeName} implementation, Dalamud.Plugin.IDalamudPluginInterface pluginInterface)");
                    using (sb.BeginBraceScope())
                    {
                        sb.AppendLine("_implementation = implementation;");

                        // Event providers
                        foreach (var eventInfo in job.Events)
                        {
                            sb.AppendLine();

                            sb.AppendLine($"_{eventInfo.NameCamelCase} = pluginInterface.GetIpcProvider<{eventInfo.MakeGenericParameterString()}>($\"{job.IpcNamespace}.{{nameof({job.InterfaceTypeName}.{eventInfo.Name})}}\");");
                            sb.AppendLine($"implementation.{eventInfo.Name} += _{eventInfo.NameCamelCase}.SendMessage;");
                        }

                        // Methods
                        foreach (var methodInfo in job.Methods)
                        {
                            sb.AppendLine();

                            sb.AppendLine($"var {methodInfo.NameCamelCase} = pluginInterface.GetIpcProvider<{methodInfo.MakeGenericParameterString()}>($\"{job.IpcNamespace}.{{nameof({job.InterfaceTypeName}.{methodInfo.Name})}}\");");
                            sb.AppendLine($"{methodInfo.NameCamelCase}.Register{(methodInfo.FullyQualifiedReturnTypeName == "void" ? "Action" : "Func")}(implementation.{methodInfo.Name});");
                            sb.AppendLine($"_providers.Add({methodInfo.NameCamelCase});");
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine("public void Dispose()");
                    using (sb.BeginBraceScope())
                    {
                        foreach (var eventInfo in job.Events)
                        {
                            sb.AppendLine($"_implementation.{eventInfo.Name} -= _{eventInfo.NameCamelCase}.SendMessage;");
                        }

                        sb.AppendLine();
                        sb.AppendLine("foreach (var provider in _providers)");
                        using (sb.BeginBraceScope())
                        {
                            sb.AppendLine("provider.UnregisterFunc();");
                            sb.AppendLine("provider.UnregisterAction();");
                        }
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"public static System.IDisposable RegisterIpcProvider({job.InterfaceTypeName} implementation, Dalamud.Plugin.IDalamudPluginInterface pluginInterface)");
                using (sb.BeginBraceScope())
                {
                    sb.AppendLine("return new Provider(implementation, pluginInterface);");
                }

                sb.AppendLine();
                sb.AppendLine($"private class Consumer : {job.InterfaceTypeName}Consumer");
                using (sb.BeginBraceScope())
                {
                    sb.AppendLine("private readonly Dalamud.Plugin.IDalamudPluginInterface _pluginInterface;");
                    
                    foreach (var eventInfo in job.Events)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"private event {eventInfo.FullyQualifiedDelegateName}? _{eventInfo.NameCamelCase};");
                        sb.AppendLine($"private Dalamud.Plugin.Ipc.ICallGateSubscriber<{eventInfo.MakeGenericParameterString()}>? _{eventInfo.NameCamelCase}Subscriber;");
                        sb.AppendLine($"public event {eventInfo.FullyQualifiedDelegateName}? {eventInfo.Name}");
                        using (sb.BeginBraceScope())
                        {
                            sb.AppendLine("add");
                            using (sb.BeginBraceScope())
                            {
                                sb.AppendLine($"if (_{eventInfo.NameCamelCase}Subscriber == null)");
                                using (sb.BeginBraceScope())
                                {
                                    sb.AppendLine($"_{eventInfo.NameCamelCase}Subscriber = _pluginInterface.GetIpcSubscriber<{eventInfo.MakeGenericParameterString()}>($\"{job.IpcNamespace}.{{nameof({job.InterfaceTypeName}.{eventInfo.Name})}}\");");
                                    sb.AppendLine($"_{eventInfo.NameCamelCase}Subscriber.Subscribe(On{eventInfo.Name});");
                                }

                                sb.AppendLine();
                                sb.AppendLine($"_{eventInfo.NameCamelCase} += value;");
                            }
                            sb.AppendLine("remove");
                            using (sb.BeginBraceScope())
                            {
                                sb.Append($"_{eventInfo.NameCamelCase} -= value;");
                            }
                        }
                    }

                    sb.AppendLine();
                    foreach (var methodInfo in job.Methods)
                    {
                        sb.AppendLine($"private Dalamud.Plugin.Ipc.ICallGateSubscriber<{methodInfo.MakeGenericParameterString()}>? _{methodInfo.NameCamelCase};");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"public Consumer(Dalamud.Plugin.IDalamudPluginInterface pluginInterface)");
                    using (sb.BeginBraceScope())
                    {
                        sb.AppendLine("_pluginInterface = pluginInterface;");
                    }

                    foreach (var eventInfo in job.Events)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"private void On{eventInfo.Name}({eventInfo.MakeParameterDeclarationString()})");
                        using (sb.BeginBraceScope())
                        {
                            sb.AppendLine($"_{eventInfo.NameCamelCase}?.Invoke({eventInfo.MakeParameterString()});");
                        }
                    }

                    foreach (var methodInfo in job.Methods)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"public {methodInfo.FullyQualifiedReturnTypeName} {methodInfo.Name}({methodInfo.MakeParameterDeclarationString()})");
                        using (sb.BeginBraceScope())
                        {
                            if (methodInfo.FullyQualifiedReturnTypeName != "void")
                            {
                                sb.Append("return ");
                            }

                            sb.AppendLine($"(_{methodInfo.NameCamelCase} ??= _pluginInterface.GetIpcSubscriber<{methodInfo.MakeGenericParameterString()}>($\"{job.IpcNamespace}.{{nameof({job.InterfaceTypeName}.{methodInfo.Name})}}\")).Invoke{(methodInfo.FullyQualifiedReturnTypeName == "void" ? "Action" : "Func")}({methodInfo.MakeParameterString()});");
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine("public void Dispose()");
                    using (sb.BeginBraceScope())
                    {
                        foreach (var eventInfo in job.Events)
                        {
                            sb.AppendLine($"_{eventInfo.NameCamelCase}Subscriber?.Unsubscribe(On{eventInfo.Name});");
                        }
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"public static {job.InterfaceTypeName}Consumer CreateIpcClient(Dalamud.Plugin.IDalamudPluginInterface pluginInterface)");
                using (sb.BeginBraceScope())
                {
                    sb.AppendLine("return new Consumer(pluginInterface);");
                }
            }
        }

        context.AddSource($"{job.Namespace.Substring("global::".Length)}.{job.InterfaceTypeName}.generated", sb.ToString());

        foreach (var message in job.Messages)
        {
            context.ReportDiagnostic(Diagnostic.Create(message.Descriptor, Location.Create(message.SourceFilename, message.Span, message.LineSpan)));
        }
    }
}

