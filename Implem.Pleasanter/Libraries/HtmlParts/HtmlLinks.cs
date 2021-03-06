﻿using Implem.Libraries.DataSources.SqlServer;
using Implem.Libraries.Utilities;
using Implem.Pleasanter.Libraries.DataSources;
using Implem.Pleasanter.Libraries.Html;
using Implem.Pleasanter.Libraries.Responses;
using Implem.Pleasanter.Libraries.Security;
using Implem.Pleasanter.Libraries.Server;
using Implem.Pleasanter.Libraries.Settings;
using Implem.Pleasanter.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;
namespace Implem.Pleasanter.Libraries.HtmlParts
{
    public static class HtmlLinks
    {
        public static HtmlBuilder Links(this HtmlBuilder hb, SiteSettings ss, long id)
        {
            var targets = Targets(id);
            var dataSet = DataSet(ss, targets);
            return Contains(ss, dataSet)
                ? hb.FieldSet(
                    css: " enclosed",
                    legendText: Displays.Links(),
                    action: () => hb
                        .Links(
                            targets: targets,
                            ss: ss,
                            dataSet: dataSet))
                : hb;
        }

        private static EnumerableRowCollection<DataRow> Targets(long linkId)
        {
            return Rds.ExecuteTable(statements: new SqlStatement[]
            {
                Rds.SelectLinks(
                    column: Rds.LinksColumn()
                        .SourceId(_as: "Id")
                        .Add("'Source' as [Direction]"),
                    where: Rds.LinksWhere().DestinationId(linkId)),
                Rds.SelectLinks(
                    unionType: Sqls.UnionTypes.UnionAll,
                    column: Rds.LinksColumn()
                        .DestinationId(_as: "Id")
                        .Add("'Destination' as [Direction]"),
                    where: Rds.LinksWhere().SourceId(linkId))
            }).AsEnumerable();
        }

        private static DataSet DataSet(SiteSettings ss, EnumerableRowCollection<DataRow> targets)
        {
            var statements = new List<SqlStatement>();
            ss.Sources.ForEach(currentSs =>
                statements.Add(SelectIssues(currentSs, targets, "Source")));
            ss.Destinations.ForEach(currentSs =>
                statements.Add(SelectIssues(currentSs, targets, "Destination")));
            ss.Sources.ForEach(currentSs =>
                statements.Add(SelectResults(currentSs, targets, "Source")));
            ss.Destinations.ForEach(currentSs =>
                statements.Add(SelectResults(currentSs, targets, "Destination")));
            return statements.Any()
                ? Rds.ExecuteDataSet(statements: statements.ToArray())
                : null;
        }

        private static SqlStatement SelectIssues(
            SiteSettings ss, EnumerableRowCollection<DataRow> targets, string direction)
        {
            return Rds.SelectIssues(
                dataTableName: "Issues" + "_" + direction + ss.SiteId,
                column: IssuesLinkColumns(ss)
                    .Add(
                        "(select [title] from [items] where [ReferenceId]=[IssueId])",
                        _as: "ItemTitle")
                    .Add("'" + direction + "' as Direction"),
                join: Rds.IssuesJoinDefault()
                    .Add(
                        tableName: "Sites",
                        joinType: SqlJoin.JoinTypes.Inner,
                        joinExpression: "[Sites].[SiteId]=[Issues].[SiteId]"),
                where: Rds.IssuesWhere()
                    .SiteId(ss.SiteId)
                    .IssueId_In(targets
                        .Where(o => o["Direction"].ToString() == direction)
                        .Select(o => o["Id"].ToLong()))
                    .CanRead("[Issues].[IssueId]"));
        }

        public static Rds.IssuesColumnCollection IssuesLinkColumns(SiteSettings ss)
        {
            var column = Rds.IssuesColumn()
                .SiteId()
                .IssueId();
            ss.LinkColumns.ForEach(columnName => column.IssuesColumn(columnName));
            return column;
        }

        private static SqlStatement SelectResults(
            SiteSettings ss, EnumerableRowCollection<DataRow> targets, string direction)
        {
            return Rds.SelectResults(
                dataTableName: "Results" + "_" + direction + ss.SiteId,
                column: ResultsLinkColumns(ss)
                    .Add(
                        "(select [title] from [items] where [ReferenceId]=[ResultId])",
                        _as: "ItemTitle")
                    .Add("'" + direction + "' as Direction"),
                join: Rds.ResultsJoinDefault()
                    .Add(
                        tableName: "Sites",
                        joinType: SqlJoin.JoinTypes.Inner,
                        joinExpression: "[Sites].[SiteId]=[Results].[SiteId]"),
                where: Rds.ResultsWhere()
                    .SiteId(ss.SiteId)
                    .ResultId_In(targets
                        .Where(o => o["Direction"].ToString() == direction)
                        .Select(o => o["Id"].ToLong()))
                    .CanRead("[Results].[ResultId]"));
        }

        public static Rds.ResultsColumnCollection ResultsLinkColumns(SiteSettings ss)
        {
            var column = Rds.ResultsColumn()
                .SiteId()
                .ResultId();
            ss.LinkColumns.ForEach(columnName => column.ResultsColumn(columnName));
            return column;
        }

        private static bool Contains(SiteSettings ss, DataSet dataSet)
        {
            if (Contains(dataSet, ss.Destinations, "Destination")) return true;
            if (Contains(dataSet, ss.Sources, "Source")) return true;
            return false;
        }

        private static bool Contains(
            DataSet dataSet, IEnumerable<SiteSettings> ssList, string direction)
        {
            foreach (var ss in ssList)
            {
                if (dataSet?.Tables[ss.ReferenceType + "_" + direction + ss.SiteId]?
                    .AsEnumerable()
                    .Where(o => o["SiteId"].ToLong() == ss.SiteId).Any() == true)
                {
                    return true;
                };
            }
            return false;
        }

        private static HtmlBuilder Links(
            this HtmlBuilder hb,
            EnumerableRowCollection<DataRow> targets,
            SiteSettings ss,
            DataSet dataSet)
        {
            return hb.Div(action: () => hb
                .LinkTables(
                    ssList: ss.Destinations,
                    dataSet: dataSet,
                    direction: "Destination",
                    caption: Displays.LinkDestinations())
                .LinkTables(
                    ssList: ss.Sources,
                    dataSet: dataSet,
                    direction: "Source",
                    caption: Displays.LinkSources()));
        }

        private static HtmlBuilder LinkTables(
            this HtmlBuilder hb,
            IEnumerable<SiteSettings> ssList,
            DataSet dataSet,
            string direction,
            string caption)
        {
            ssList.ForEach(ss => hb.Table(
                css: "grid",
                attributes: new HtmlAttributes().DataValue("back"),
                action: () =>
                {
                    var dataRows = dataSet.Tables[ss.ReferenceType + "_" + direction + ss.SiteId]?
                        .AsEnumerable()
                        .Where(o => o["SiteId"].ToLong() == ss.SiteId);
                    var siteMenu = SiteInfo.TenantCaches[Sessions.TenantId()].SiteMenu;
                    if (dataRows != null && dataRows.Any())
                    {
                        ss.SetColumnAccessControls();
                        var columns = ss.GetLinkColumns(checkPermission: true);
                        switch (ss.ReferenceType)
                        {
                            case "Issues":
                                var issueCollection = new IssueCollection(ss, dataRows);
                                issueCollection.SetLinks(ss);
                                hb
                                    .Caption(caption: "{0} : {1} - {2} {3}".Params(
                                        caption,
                                        siteMenu.Breadcrumb(ss.SiteId)
                                            .Select(o => o.Title)
                                            .Join(" > "),
                                        Displays.Quantity(),
                                        dataRows.Count()))
                                    .THead(action: () => hb
                                        .GridHeader(columns: columns, sort: false, checkRow: false))
                                    .TBody(action: () => issueCollection
                                        .ForEach(issueModel =>
                                        {
                                            ss.SetColumnAccessControls(issueModel.Mine());
                                            hb.Tr(
                                                attributes: new HtmlAttributes()
                                                    .Class("grid-row")
                                                    .DataId(issueModel.IssueId.ToString()),
                                                action: () => columns
                                                    .ForEach(column => hb
                                                        .TdValue(
                                                            ss: ss,
                                                            column: column,
                                                            issueModel: issueModel)));
                                        }));
                                break;
                            case "Results":
                                var resultCollection = new ResultCollection(ss, dataRows);
                                resultCollection.SetLinks(ss);
                                hb
                                    .Caption(caption: "{0} : {1} - {2} {3}".Params(
                                        caption,
                                        siteMenu.Breadcrumb(ss.SiteId)
                                            .Select(o => o.Title)
                                            .Join(" > "),
                                        Displays.Quantity(),
                                        dataRows.Count()))
                                    .THead(action: () => hb
                                        .GridHeader(columns: columns, sort: false, checkRow: false))
                                    .TBody(action: () => resultCollection
                                        .ForEach(resultModel =>
                                        {
                                            ss.SetColumnAccessControls(resultModel.Mine());
                                            hb.Tr(
                                                attributes: new HtmlAttributes()
                                                    .Class("grid-row")
                                                    .DataId(resultModel.ResultId.ToString()),
                                                action: () => columns
                                                    .ForEach(column => hb
                                                        .TdValue(
                                                            ss: ss,
                                                            column: column,
                                                            resultModel: resultModel)));
                                        }));
                                break;
                        }
                    }
                }));
            return hb;
        }
    }
}
