using CaseInterview.Application.Features.Search.DTOs;
using FluentValidation;

namespace CaseInterview.Application.Features.Search.Validators;

public class BookSearchRequestValidator : AbstractValidator<BookSearchRequest>
{
    public BookSearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query is required.")
            .MaximumLength(1000).WithMessage("Query cannot exceed 1000 characters.");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 50).WithMessage("TopK must be between 1 and 50.");
    }
}
