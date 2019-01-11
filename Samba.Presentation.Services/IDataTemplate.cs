using Samba.Infrastructure.Data;
using Samba.Presentation.Services.Common.DataGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Samba.Presentation.Services
{
    public interface IDataTemplate
    {
        void Create(DataCreationService dataCreationService, IWorkspace _workspace);
    }
}
