using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaFi.WebShareCz.TcPlugin
{
    internal interface IDisposableEnumerable<T> : IEnumerable<T>, IDisposable
    {
    }
}
