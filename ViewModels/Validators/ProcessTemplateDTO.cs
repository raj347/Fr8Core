﻿using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Data.Interfaces.DataTransferObjects;

namespace HubWeb.ViewModels.Validators
{
    public class RouteDTOValidator : AbstractValidator<RouteEmptyDTO>
    {
        public RouteDTOValidator()
        {
            RuleFor(ptdto => ptdto.Name).NotNull();
            RuleFor(ptdto => ptdto.Name).NotEmpty();
        }
    }
}