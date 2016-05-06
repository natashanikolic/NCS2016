using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileConversionCommon
{
    public class FileContext : DbContext
    {
        //this constructor is used by the web project getting connection string from web config
        public FileContext()
            : base("name=FileConversionContext")
        {
        }

        //this constructor is used by the worker role
        public FileContext(string connString)
            : base(connString)
        {
            //((IObjectContextAdapter)this).ObjectContext.CommandTimeout = 180;
        }

        public System.Data.Entity.DbSet<File> Files { get; set; }
    }
}
