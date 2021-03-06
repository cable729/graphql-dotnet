﻿using System;
using GraphQL.Types;
using Should;

namespace GraphQL.Tests.Types
{
    public class FieldRegistrationTests
    {
        [Test]
        public void throws_error_when_trying_to_register_field_with_same_name()
        {
            var graphType = new ObjectGraphType();
            graphType.Field<StringGraphType>("id");

            Expect.Throws<ArgumentOutOfRangeException>(
                () => graphType.Field<StringGraphType>("id")
            );
        }

        [Test]
        public void can_register_field_of_compatible_type()
        {
            var graphType = new ObjectGraphType();
            graphType.Field(typeof(BooleanGraphType), "isValid").Type.ShouldEqual(typeof(BooleanGraphType));
        }

        [Test]
        public void throws_error_when_trying_to_register_field_of_incompatible_type()
        {
            var graphType = new ObjectGraphType();

            Expect.Throws<ArgumentOutOfRangeException>(
                () => graphType.Field(typeof(string), "id")
            );
        }
    }
}
