using Microsoft.AspNetCore.Mvc.Testing;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TaskManagement.Tests")]

[assembly: WebApplicationFactoryContentRoot(
    "TaskManagement.API, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    "../../../../../Backend/src/TaskManagement.API",
    "TaskManagement.API.csproj",
    "0")]
