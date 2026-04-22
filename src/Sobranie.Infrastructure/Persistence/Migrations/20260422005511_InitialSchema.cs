using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sobranie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BeefEdges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromMPId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ToMPId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeefEdges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmCallLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PromptHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PromptPreview = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Output = table.Column<string>(type: "TEXT", nullable: true),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    PrefillSeconds = table.Column<double>(type: "REAL", nullable: false),
                    GenerationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    Rejected = table.Column<bool>(type: "INTEGER", nullable: false),
                    RejectReason = table.Column<string>(type: "TEXT", nullable: true),
                    CalledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmCallLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    PartyId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    SeatCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.PartyId);
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Headline = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    RewrittenAsProposal = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IntroducedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChorusLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartyId = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    TopicTag = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChorusLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChorusLines_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "PartyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MPs",
                columns: table => new
                {
                    MPId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PartyId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    Aggression = table.Column<double>(type: "REAL", nullable: false),
                    Legalism = table.Column<double>(type: "REAL", nullable: false),
                    Populism = table.Column<double>(type: "REAL", nullable: false),
                    SeatIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonaSystemPrompt = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MPs", x => x.MPId);
                    table.ForeignKey(
                        name: "FK_MPs_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "PartyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignatureMoves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MPId = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Exemplar = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    TriggerWeight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureMoves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureMoves_MPs_MPId",
                        column: x => x.MPId,
                        principalTable: "MPs",
                        principalColumn: "MPId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Speeches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MPId = table.Column<string>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    UtteredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    UtilityAtSelection = table.Column<double>(type: "REAL", nullable: true),
                    OutputFilterRejected = table.Column<bool>(type: "INTEGER", nullable: false),
                    RejectReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Speeches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Speeches_MPs_MPId",
                        column: x => x.MPId,
                        principalTable: "MPs",
                        principalColumn: "MPId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Speeches_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeefEdges_FromMPId_ToMPId",
                table: "BeefEdges",
                columns: new[] { "FromMPId", "ToMPId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChorusLines_PartyId_TopicTag",
                table: "ChorusLines",
                columns: new[] { "PartyId", "TopicTag" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmCallLogs_CalledAt",
                table: "LlmCallLogs",
                column: "CalledAt");

            migrationBuilder.CreateIndex(
                name: "IX_MPs_PartyId",
                table: "MPs",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_MPs_Tier_PartyId",
                table: "MPs",
                columns: new[] { "Tier", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FetchedAt",
                table: "Proposals",
                column: "FetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Status",
                table: "Proposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureMoves_MPId",
                table: "SignatureMoves",
                column: "MPId");

            migrationBuilder.CreateIndex(
                name: "IX_Speeches_MPId_UtteredAt",
                table: "Speeches",
                columns: new[] { "MPId", "UtteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Speeches_ProposalId",
                table: "Speeches",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_Speeches_UtteredAt",
                table: "Speeches",
                column: "UtteredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeefEdges");

            migrationBuilder.DropTable(
                name: "ChorusLines");

            migrationBuilder.DropTable(
                name: "LlmCallLogs");

            migrationBuilder.DropTable(
                name: "SignatureMoves");

            migrationBuilder.DropTable(
                name: "Speeches");

            migrationBuilder.DropTable(
                name: "MPs");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
