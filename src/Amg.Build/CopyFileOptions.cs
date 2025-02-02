﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amg.Build
{
    class CopyFileOptions
    {
        public bool AllowDecryptedDestination { get; set; }
        public bool CopySymlink { get; set; }
        public bool FailIfExists { get; set; }
        public bool NoBuffering { get; set; }
        public bool OpenSourceForWrite { get; set; }
        public bool Restartable { get; set; }

        /// <summary>
        /// Negation of FailIfExists
        /// </summary>
        public bool Overwrite
        {
            get
            {
                return !FailIfExists;
            }

            set
            {
                FailIfExists = !value;
            }
        }
    }
}
