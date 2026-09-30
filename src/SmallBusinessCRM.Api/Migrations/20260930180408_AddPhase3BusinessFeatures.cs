using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmallBusinessCRM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3BusinessFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiScoreExplanation",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ConversionProbability",
                table: "Leads",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DripCampaignStep",
                table: "Leads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDripPaused",
                table: "Leads",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDripEmailSentUtc",
                table: "Leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredictedCloseDays",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalendarIntegrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalendarId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsSyncEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarIntegrations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailTrackings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOpened = table.Column<bool>(type: "bit", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClickCount = table.Column<int>(type: "int", nullable: false),
                    LastClickedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastClickedUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTrackings_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailTrackings_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarIntegrations_UserId",
                table: "CalendarIntegrations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTrackings_LeadId",
                table: "EmailTrackings",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTrackings_TaskId",
                table: "EmailTrackings",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarIntegrations");

            migrationBuilder.DropTable(
                name: "EmailTrackings");

            migrationBuilder.DropColumn(
                name: "AiScoreExplanation",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ConversionProbability",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "DripCampaignStep",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "IsDripPaused",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LastDripEmailSentUtc",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PredictedCloseDays",
                table: "Leads");
        }
    }
}
