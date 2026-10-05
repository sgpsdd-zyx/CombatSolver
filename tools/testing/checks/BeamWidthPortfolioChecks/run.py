"""Compile the production beam-width portfolio combinator and its refinement gate against
controlled member runs.

The combinator and pure quality policies are copied verbatim. The interim result record,
theft recovery order and coordinator entry are extracted from their production files.
"""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[4]
out = root / '.local/beam-width-portfolio-checks'
out.mkdir(parents=True, exist_ok=True)


def block(path, declaration):
    text = path.read_text(encoding='utf-8')
    start = text.index(declaration)
    opening = text.index('{', start)
    expression = text.find('=>', start, opening)
    if expression != -1:
        return text[start:text.index(';', expression) + 1]
    end, depth = opening + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


search = root / 'src/Search'
for filename in ('RouteQuality.cs', 'RouteQualityPolicy.cs', 'SolverInterimResultOrdering.cs'):
    (out / filename).write_text((search / filename).read_text(encoding='utf-8'), encoding='utf-8')
(out / 'BeamWidthPortfolio.cs').write_text(
    (search / 'BeamWidthPortfolio.cs').read_text(encoding='utf-8'), encoding='utf-8')
(out / 'BeamWidthPortfolioGate.cs').write_text(
    (search / 'BeamWidthPortfolioGate.cs').read_text(encoding='utf-8'), encoding='utf-8')
(out / 'PowerCommitmentPortfolioGate.cs').write_text(
    (search / 'PowerCommitmentPortfolioGate.cs').read_text(encoding='utf-8'), encoding='utf-8')
(out / 'PowerCommitmentSeatPolicy.cs').write_text(
    (search / 'PowerCardValuation/Commitments/PowerCommitmentSeatPolicy.cs').read_text(encoding='utf-8'), encoding='utf-8')
(out / 'SolverSearchProfile.cs').write_text(
    (search / 'SolverSearchProfile.cs').read_text(encoding='utf-8'), encoding='utf-8')
# Opaque optional reference only; these combinator checks never evaluate a learned model.
(out / 'ContextualRankingModel.cs').write_text(
    'namespace CombatSolver; internal sealed class ContextualRankingModel {}\n', encoding='utf-8')
(out / 'Ordering.cs').write_text(
    'namespace CombatSolver;\n'
    + block(search / 'TheftEncounterStrategy.cs', 'internal enum SolverTheftPolicy') + '\n'
    + block(root / 'src/Runtime/SolverProgress.cs', 'internal sealed record SolverInterimResult(') + '\n'
    + 'internal static class TheftEncounterStrategy {\n'
    + block(search / 'TheftEncounterStrategy.cs', '    public static int CompareRecovery(SolverTheftPolicy? policy,') + '\n}\n'
    + 'internal static partial class CombatSearchCoordinator {\n'
    + block(search / 'CombatSearchCoordinator.cs',
            '    internal static bool IsBetterPotionPolicyResult(\n        SolverTheftPolicy? theftPolicy,') + '\n}\n',
    encoding='utf-8')
(out / 'Program.cs').write_text(
    (Path(__file__).parent / 'Program.cs').read_text(encoding='utf-8'), encoding='utf-8')
(out / 'Checks.csproj').write_text(
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
    '<TargetFramework>net9.0</TargetFramework><LangVersion>13.0</LangVersion><Nullable>enable</Nullable>'
    '<ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>'
    '</PropertyGroup></Project>', encoding='utf-8')
subprocess.run(['dotnet', 'run', '--project', str(out / 'Checks.csproj'), '-c', 'Release'], check=True)
