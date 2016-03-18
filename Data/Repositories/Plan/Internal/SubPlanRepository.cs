﻿using Data.Entities;
using Data.Interfaces;

namespace Data.Repositories
{
    public class SubPlanRepository : GenericRepository<SubPlanDO>, ISubPlanRepository
    {
        internal SubPlanRepository(IUnitOfWork uow)
            : base(uow)
        {
        }
    }

    public interface ISubPlanRepository : IGenericRepository<SubPlanDO>
    {
    }
}