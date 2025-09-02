namespace MMM;

interface IVersionRange
{
    bool Satisfies(string version);
    bool Satisfies(SemanticVersion version);
}
