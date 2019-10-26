﻿using FluentValidation;

namespace Artemis.UI.ViewModels.Dialogs
{
    public class SurfaceCreateViewModelValidator : AbstractValidator<SurfaceCreateViewModel>
    {
        public SurfaceCreateViewModelValidator()
        {
            RuleFor(m => m.SurfaceName).NotEmpty().WithMessage("Layout name may not be empty");
        }
    }
}