using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastPort.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEFCoreWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_UserSubscriptions_UserSubscriptionId1",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_UserSubscriptionId1",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId1",
                table: "PaymentTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId1",
                table: "PaymentTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_UserSubscriptionId1",
                table: "PaymentTransactions",
                column: "UserSubscriptionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_UserSubscriptions_UserSubscriptionId1",
                table: "PaymentTransactions",
                column: "UserSubscriptionId1",
                principalTable: "UserSubscriptions",
                principalColumn: "Id");
        }
    }
}
