using FluentAssertions;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Entities.Evidence;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;
using Xunit;

namespace TraceCore.UnitTests.Domain;

public class EvidenceTests
{
    private readonly Guid _caseId = Guid.NewGuid();
    private readonly Guid _officer1 = Guid.NewGuid();
    private readonly Guid _officer2 = Guid.NewGuid();
    private const string SampleSha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

    [Fact]
    public void Collect_ShouldCreateGenesisCustodyEvent_WithChainedHash()
    {
        // Act
        var evidence = Evidence.Collect(
            _caseId,
            "EVD-2026-0001",
            EvidenceType.Physical,
            "Recovered USB flash drive",
            "Desk in Room 102",
            _officer1,
            ConfidentialityLevel.Confidential,
            "Locker 4A",
            SampleSha256,
            true,
            "Found inside sealed envelope.");

        // Assert
        evidence.EvidenceNumber.Should().Be("EVD-2026-0001");
        evidence.Status.Should().Be(EvidenceStatus.InCustody);
        evidence.CustodyEvents.Should().HaveCount(1);

        var genesis = evidence.CustodyEvents.First();
        genesis.Action.Should().Be(CustodyAction.Collected);
        genesis.FromUserId.Should().Be(_officer1);
        genesis.ToUserId.Should().Be(_officer1);
        genesis.PreviousHash.Should().Be("0000000000000000000000000000000000000000000000000000000000000000");
        genesis.CurrentHash.Should().NotBeNullOrWhiteSpace();

        evidence.DomainEvents.Should().ContainSingle(e => e is EvidenceAddedDomainEvent);
    }

    [Fact]
    public void RecordTransfer_ShouldAppendCustodyEvent_AndChainPreviousHash()
    {
        // Arrange
        var evidence = Evidence.Collect(
            _caseId,
            "EVD-2026-0002",
            EvidenceType.Digital,
            "Hard Drive Image",
            "Server Rack Alpha",
            _officer1,
            ConfidentialityLevel.Restricted,
            "Vault 1",
            SampleSha256,
            false);

        var genesisHash = evidence.CustodyEvents.First().CurrentHash;

        // Act
        evidence.RecordTransfer(
            _officer1,
            _officer2,
            CustodyAction.CheckedOutForAnalysis,
            "Forensics Lab Station 3",
            "Forensic bitstream analysis.",
            SampleSha256);

        // Assert
        evidence.CustodyEvents.Should().HaveCount(2);
        evidence.Status.Should().Be(EvidenceStatus.InAnalysis);
        evidence.StorageLocation.Should().Be("Forensics Lab Station 3");

        var secondEvent = evidence.CustodyEvents.Last();
        secondEvent.PreviousHash.Should().Be(genesisHash);
        secondEvent.CurrentHash.Should().NotBe(genesisHash);
        secondEvent.FromUserId.Should().Be(_officer1);
        secondEvent.ToUserId.Should().Be(_officer2);

        evidence.DomainEvents.Should().Contain(e => e is EvidenceCustodyTransferredDomainEvent);
    }

    [Fact]
    public void RecordTransfer_WithMismatchedHash_ShouldThrowDomainException_PreventingTampering()
    {
        // Arrange
        var evidence = Evidence.Collect(
            _caseId,
            "EVD-2026-0003",
            EvidenceType.Document,
            "Financial Ledger",
            "Accounting Office",
            _officer1,
            ConfidentialityLevel.Internal,
            "File Cabinet B",
            SampleSha256,
            false);

        string tamperedHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

        // Act & Assert
        var act = () => evidence.RecordTransfer(
            _officer1,
            _officer2,
            CustodyAction.Transferred,
            "Secure Archive",
            "Transfer",
            tamperedHash);

        act.Should().Throw<DomainException>()
            .WithMessage("*Hash mismatch*tampering detected*");
    }
}
