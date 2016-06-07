using System.Collections.Generic;
using System.Linq;
using GraphQL.Language;
using GraphQL.Types;
using GraphQL.Validation.Rules;

namespace GraphQL.Validation
{
    public interface IDocumentValidator
    {
        IValidationResult Validate(
            ISchema schema,
            Document document,
            IEnumerable<IValidationRule> rules = null);
    }

    public class DocumentValidator : IDocumentValidator
    {
        public IValidationResult Validate(
            ISchema schema,
            Document document,
            IEnumerable<IValidationRule> rules = null)
        {
            var context = new ValidationContext
            {
                Schema = schema,
                Document = document,
                TypeInfo = new TypeInfo(schema)
            };

            if (rules == null)
            {
//                rules = Rules();
                rules = new List<IValidationRule>();
            }

            var visitors = rules.Select(x => x.Validate(context)).ToList();

            visitors.Insert(0, context.TypeInfo);

            var basic = new BasicVisitor(visitors);

            basic.Visit(document);

            var result = new ValidationResult();
            result.Errors.AddRange(context.Errors);
            return result;
        }

        private List<IValidationRule> Rules()
        {
            var rules = new List<IValidationRule>()
            {
                new UniqueOperationNames(),
                new LoneAnonymousOperationRule(),
                new ScalarLeafs()
            };
            return rules;
        }
    }
}
