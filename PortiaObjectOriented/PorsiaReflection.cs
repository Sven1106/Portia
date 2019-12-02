using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented
{
    //public class PorsiaReflection
    //{
    //    public void DeserializeJson(string json)
    //    {
    //        Request portiaRequest = JsonConvert.DeserializeObject<Request>(json);
    //        foreach (var dataEntry in portiaRequest.Data)
    //        {

    //            List<ClassDefinition> classDefinitions = new List<ClassDefinition>();
    //            foreach (var attribute in dataEntry.Items)
    //            {
    //                ClassDefinition classDefinition = CreateKeyValuePairFromAttribute(attribute);
    //                bool isNull = classDefinition.Equals(new KeyValuePair<string, Type>());
    //                if (isNull == false)
    //                {
    //                    classDefinitions.Add(classDefinition);
    //                }
    //            }
    //            MyClassBuilder myClassBuilder = new MyClassBuilder(dataEntry.TaskName);
    //            var myclass = myClassBuilder.CreateObject(classDefinitions);
    //            Type type = myclass.GetType();
    //            WriteAllPropertyNames(type);
    //            Console.ReadKey();
    //        }
    //    }

    //    private static void WriteAllPropertyNames(Type type, string indent = "")
    //    {
    //        indent += "--";
    //        foreach (PropertyInfo property in type.GetProperties())
    //        {
    //            Type propertyType = property.PropertyType;
    //            if (property.Module.ScopeName == "CommonLanguageRuntimeLibrary")
    //            {
    //                continue;
    //            }
    //            else
    //            {
    //                XpathAttribute[] xpathAttributes = (XpathAttribute[])property.GetCustomAttributes(typeof(XpathAttribute), false);
    //                Console.WriteLine(indent + property.Name + ":" + propertyType + ":" + xpathAttributes[0].NodeXpath);
    //                if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
    //                {
    //                    foreach (var item in propertyType.GetProperties())
    //                    {
    //                        WriteAllPropertyNames(item.PropertyType, indent);
    //                    }
    //                }
    //                else
    //                {
    //                    WriteAllPropertyNames(propertyType, indent);
    //                }
    //            }

    //        }
    //    }

    //    private static ClassDefinition CreateKeyValuePairFromAttribute(NodeAttribute node)
    //    {
    //        Type type;
    //        if (node.Type.ToLower() == "string")
    //        {
    //            type = typeof(string);
    //        }
    //        else if (node.Type.ToLower() == "number")
    //        {
    //            type = typeof(decimal);
    //        }
    //        else if (node.Type.ToLower() == "boolean")
    //        {
    //            type = typeof(bool);
    //        }
    //        else if (node.Type.ToLower() == "object" && node.Attributes.Count() > 0)
    //        {
    //            List<ClassDefinition> classDefinitions = new List<ClassDefinition>();
    //            foreach (NodeAttribute attribute in node.Attributes)
    //            {
    //                classDefinitions.Add(CreateKeyValuePairFromAttribute(attribute));
    //            }
    //            MyClassBuilder myClassBuilder = new MyClassBuilder(node.Name);
    //            var myclass = myClassBuilder.CreateObject(classDefinitions);
    //            type = myclass.GetType();
    //        }
    //        else
    //        { // TODO add prober error handling. return list of errors that occurred.
    //            return new ClassDefinition(); // returns empty value if provided type wasn't valid.
    //        }
    //        if (node.GetMultipleFromPage)
    //        {
    //            var listType = typeof(List<>);
    //            type = listType.MakeGenericType(type);
    //        }
    //        return new ClassDefinition(node.Name, type, node.Xpath);
    //    }
    //    private class ClassDefinition
    //    {
    //        public string PropertyName { get; set; }
    //        public Type PropertyType { get; set; }
    //        public string Xpath { get; set; }
    //        public ClassDefinition()
    //        {

    //        }
    //        public ClassDefinition(string propertyName, Type propertyType, string xpath)
    //        {
    //            PropertyName = propertyName;
    //            PropertyType = propertyType;
    //            Xpath = xpath;
    //        }
    //    }
    //    private class MyClassBuilder
    //    {
    //        AssemblyName assemblyName;
    //        public MyClassBuilder(string className)
    //        {
    //            assemblyName = new AssemblyName(className);
    //        }
    //        public object CreateObject(List<ClassDefinition> objectDefinitions) // TODO Change back to arrays of PropertyNames, PropertyTypes and PropertyCustromAttributes
    //        {

    //            TypeBuilder DynamicClass = this.CreateClass();
    //            this.CreateConstructor(DynamicClass);
    //            foreach (var objectDefinition in objectDefinitions)
    //            {
    //                var attrCtorParams = new Type[] { typeof(string) };
    //                var attrCtorInfo = typeof(XpathAttribute).GetConstructor(attrCtorParams);
    //                var attrBuilder = new CustomAttributeBuilder(attrCtorInfo, new object[] { objectDefinition.Xpath });

    //                CreateProperty(DynamicClass, objectDefinition.PropertyName, objectDefinition.PropertyType, attrBuilder);
    //            }
    //            Type type = DynamicClass.CreateType();
    //            return Activator.CreateInstance(type);
    //        }
    //        private TypeBuilder CreateClass()
    //        {
    //            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(this.assemblyName, AssemblyBuilderAccess.Run);
    //            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("dynamicModule");
    //            TypeBuilder typeBuilder = moduleBuilder.DefineType(this.assemblyName.FullName
    //                                , TypeAttributes.Public |
    //                                TypeAttributes.Class |
    //                                TypeAttributes.AutoClass |
    //                                TypeAttributes.AnsiClass |
    //                                TypeAttributes.BeforeFieldInit |
    //                                TypeAttributes.AutoLayout
    //                                , null);
    //            return typeBuilder;
    //        }
    //        private void CreateConstructor(TypeBuilder typeBuilder)
    //        {
    //            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
    //        }
    //        private void CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType, CustomAttributeBuilder attrBuilder)
    //        {
    //            FieldBuilder fieldBuilder = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

    //            PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
    //            propertyBuilder.SetCustomAttribute(attrBuilder);
    //            MethodBuilder getPropMthdBldr = typeBuilder.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
    //            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

    //            getIl.Emit(OpCodes.Ldarg_0);
    //            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
    //            getIl.Emit(OpCodes.Ret);

    //            MethodBuilder setPropMthdBldr = typeBuilder.DefineMethod("set_" + propertyName,
    //                  MethodAttributes.Public |
    //                  MethodAttributes.SpecialName |
    //                  MethodAttributes.HideBySig,
    //                  null, new[] { propertyType });

    //            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
    //            Label modifyProperty = setIl.DefineLabel();
    //            Label exitSet = setIl.DefineLabel();

    //            setIl.MarkLabel(modifyProperty);
    //            setIl.Emit(OpCodes.Ldarg_0);
    //            setIl.Emit(OpCodes.Ldarg_1);
    //            setIl.Emit(OpCodes.Stfld, fieldBuilder);

    //            setIl.Emit(OpCodes.Nop);
    //            setIl.MarkLabel(exitSet);
    //            setIl.Emit(OpCodes.Ret);

    //            propertyBuilder.SetGetMethod(getPropMthdBldr);
    //            propertyBuilder.SetSetMethod(setPropMthdBldr);
    //        }
    //    }
    //}
}
