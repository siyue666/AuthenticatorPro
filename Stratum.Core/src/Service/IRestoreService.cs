// Copyright (C) 2022 jmh
// SPDX-License-Identifier: GPL-3.0-only

using System.Threading.Tasks;
using Stratum.Core.Backup;

namespace Stratum.Core.Service
{
    public interface IRestoreService
    {
        public Task<RestoreResult> RestoreAsync(Backup.Backup backup);
        public Task<RestoreResult> RestoreAndUpdateAsync(Backup.Backup backup);
    }
}