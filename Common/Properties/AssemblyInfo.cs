using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// 有关程序集的常规信息通过以下
// 特性集控制。更改这些特性值可修改
// 与程序集关联的信息。
[assembly: AssemblyTitle("Titans-Server UCGO Common's Library")]
[assembly: AssemblyDescription("Titans-Server UCGO Common's Library")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Titans-Server UCGO Team")]
[assembly: AssemblyProduct("Common")]
[assembly: AssemblyCopyright("Copyright © Titans-Server 2013")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// 将 ComVisible 设置为 false 使此程序集中的类型
// 对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型，
// 则将该类型上的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
[assembly: Guid("c24b5dc1-1ad1-4073-82b4-5ee4f6187315")]

// 程序集的版本信息由下面四个值组成:
//
//      主版本
//      次版本 
//      内部版本号
//      修订号
//
// 可以指定所有这些值，也可以使用“内部版本号”和“修订号”的默认值，
// 方法是按如下所示使用“*”:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.1.0.8")]
[assembly: AssemblyFileVersion("0.1.0.8")]
namespace Common
{
    public class VersionInformation
    {
        public static string Version = "8";
        public static string ModifyDate = "2013/12/03 13:39:55";
    }
}
