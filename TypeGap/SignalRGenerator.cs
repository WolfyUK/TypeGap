﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TypeGap.Extensions;
using TypeGap.Util;
using TypeLite;

namespace TypeGap
{
    public class SignalRGenerator
    {
        internal const string HUB_TYPE = "Microsoft.AspNet.SignalR.Hub";

        private string GenerateHubs(Assembly assembly, TypeConverter converter)
        {
            var hubs = assembly.GetTypes()
                .Where(t => t.GetDnxCompatible().BaseType != null && t.GetDnxCompatible().BaseType.FullName != null && t.GetDnxCompatible().BaseType.FullName.Contains(HUB_TYPE))
                .OrderBy(t => t.FullName)
                .ToList();

            if (!hubs.Any()) return "";

            var scriptBuilder = new ScriptBuilder("    ");
            // Output signalR style promise interface:
            scriptBuilder.AppendLine("interface ISignalRPromise<T> {");
            using (scriptBuilder.IncreaseIndentation())
            {
                scriptBuilder.AppendLineIndented("done(cb: (result: T) => any): ISignalRPromise<T>;");
                scriptBuilder.AppendLineIndented("error(cb: (error: any) => any): ISignalRPromise<T>;");
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            hubs.ForEach(h => GenerateHubInterfaces(h, scriptBuilder, converter));
            // Generate client connection interfaces
            scriptBuilder.AppendLineIndented("interface SignalR {");
            using (scriptBuilder.IncreaseIndentation())
            {
                hubs.ForEach(h => scriptBuilder.AppendLineIndented(h.Name.ToCamelCase() + ": I" + h.Name + "Proxy;"));
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            return scriptBuilder.ToString();
        }

        public void WriteHubs(Type[] hubs, TypeConverter converter, CustomIndentedTextWriter writer)
        {
            var hubList = hubs.ToList();
            var scriptBuilder = new ScriptBuilder("    ");
            // Output signalR style promise interface:
            scriptBuilder.AppendLine("interface ISignalRPromise<T> {");
            using (scriptBuilder.IncreaseIndentation())
            {
                scriptBuilder.AppendLineIndented("done(cb: (result: T) => any): ISignalRPromise<T>;");
                scriptBuilder.AppendLineIndented("error(cb: (error: any) => any): ISignalRPromise<T>;");
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            hubList.ForEach(h => GenerateHubInterfaces(h, scriptBuilder, converter));
            // Generate client connection interfaces
            scriptBuilder.AppendLineIndented("interface SignalR {");
            using (scriptBuilder.IncreaseIndentation())
            {
                hubList.ForEach(h => scriptBuilder.AppendLineIndented(h.Name.ToCamelCase() + ": I" + h.Name + "Proxy;"));
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();

            writer.WriteLine(scriptBuilder.ToString());
        }

        private void GenerateHubInterfaces(Type hubType, ScriptBuilder scriptBuilder, TypeConverter converter)
        {
            if (!hubType.GetDnxCompatible().BaseType.FullName.Contains(HUB_TYPE)) throw new ArgumentException("The supplied type does not appear to be a SignalR hub.", "hubType");
            // Build the client interface
            scriptBuilder.AppendLineIndented(string.Format("interface I{0}Client {{", hubType.Name));
            using (scriptBuilder.IncreaseIndentation())
            {
                if (!hubType.GetDnxCompatible().BaseType.GetDnxCompatible().IsGenericType)
                {
                    scriptBuilder.AppendLineIndented("/* Client interface not generated as hub doesn't derive from Hub<T> */");
                }
                else
                {
                    GenerateMethods(scriptBuilder, hubType.GetDnxCompatible().BaseType.GenericTypeArguments.First(), converter);
                }
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            // Build the interface containing the SERVER methods
            scriptBuilder.AppendLineIndented(string.Format("interface I{0} {{", hubType.Name));
            using (scriptBuilder.IncreaseIndentation())
            {
                GenerateMethods(scriptBuilder, hubType, converter);
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            // Build the proxy class (represents the proxy generated by signalR).
            scriptBuilder.AppendLineIndented(string.Format("interface I{0}Proxy {{", hubType.Name));
            using (scriptBuilder.IncreaseIndentation())
            {
                scriptBuilder.AppendLineIndented("server: I" + hubType.Name + ";");
                scriptBuilder.AppendLineIndented("client: I" + hubType.Name + "Client;");
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
        }
        private void GenerateMethods(ScriptBuilder scriptBuilder, Type type, TypeConverter converter)
        {
            type.GetDnxCompatible().GetMethods()
                .Where(mi => mi.GetBaseDefinition().DeclaringType.Name == type.Name)
                .OrderBy(mi => mi.Name)
                .ToList()
                .ForEach(m => scriptBuilder.AppendLineIndented(GenerateMethodDeclaration(m, converter)));
        }
        private string GenerateMethodDeclaration(MethodInfo methodInfo, TypeConverter converter)
        {
            var result = methodInfo.Name.ToCamelCase() + "(";
            result += string.Join(", ", methodInfo.GetParameters().Select(param => param.Name + ": " + converter.GetTypeScriptName(param.ParameterType)));

            var returnTypeName = converter.GetTypeScriptName(methodInfo.ReturnType);
            returnTypeName = returnTypeName == "void" ? "void" : "ISignalRPromise<" + returnTypeName + ">";
            result += "): " + returnTypeName + ";";
            return result;
        }
    }
}
