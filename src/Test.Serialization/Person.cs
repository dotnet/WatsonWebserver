namespace Test.Serialization
{
    using System;
    using System.Collections.Generic;

    public class Person
    {
        public int Age { get; set; } = 0;
        public string FirstName { get; set; } = null;
        public string LastName { get; set; } = null;
        public string Serializer { get; set; } = null;

        internal List<string> FirstNames = new List<string>
        {
            "Joel",
            "Maria",
            "Jason",
            "Sienna",
            "Maribel",
            "Salma",
            "Khaleesi",
            "Watson",
            "Jenny",
            "Jessica",
            "Jesus",
            "Lila",
            "Tuco",
            "Walter",
            "Jesse",
            "Mike"
        };

        internal List<string> LastNames = new List<string>
        {
            "Christner",
            "Sanchez",
            "Mendoza",
            "White",
            "Salamanca",
            "Pinkman"
        };

        public static Person Random(Random rand, string serializer)
        {
            Person p = new Person();
            p.Age = rand.Next(0, 100);
            p.FirstName = p.FirstNames[rand.Next(0, (p.FirstNames.Count - 1))];
            p.LastName = p.LastNames[rand.Next(0, (p.LastNames.Count - 1))];
            p.Serializer = serializer;
            return p;
        }
    }
}
