using Hansoft.Jean.Behavior;
using Hansoft.ObjectWrapper;
using HPMSdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Hansoft.Jean.Behavior.AggregateMilestoneBehavior
{
    public class AggregateMilestoneBehavior : AbstractBehavior
    {
        private const string TEAM_PROJECT_PREFIX = "Team - ";

        string title = "AggregateMilestoneBehavior";
        bool initializationOK = false;
        string projectName;
        string viewName;
        EHPMReportViewType viewType;
        List<Project> projects;
        List<ProjectView> projectViews;
        string find;
        bool inverted = false;

        public AggregateMilestoneBehavior(XmlElement configuration)
            : base(configuration)
        {
            projectName = GetParameter("HansoftProject");
            viewName = GetParameter("View");
            viewType = GetViewType(viewName);
            string invert = GetParameter("InvertedMatch");
            if (invert != null)
                inverted = invert.ToLower().Equals("yes");
            find = GetParameter("Find");
        }

        private void InitializeProjects()
        {
            projects = HPMUtilities.FindProjects(projectName, inverted);
            projectViews = new List<ProjectView>();
            foreach (Project project in projects)
            {
                ProjectView projectView;
                if (viewType == EHPMReportViewType.AgileBacklog)
                    projectView = project.ProductBacklog;
                else if (viewType == EHPMReportViewType.AllBugsInProject)
                    projectView = project.BugTracker;
                else
                    projectView = project.Schedule;

                projectViews.Add(projectView);
            }
        }

        public override void Initialize()
        {
            initializationOK = false;
            
            InitializeProjects();

            initializationOK = true;
            DoUpdate();
        }
    
        public override string Title
        {
            get { return title; }
        }

        private static bool IsTeamProject(Task task)
        {
            return task.Project.Name.StartsWith(TEAM_PROJECT_PREFIX);
        }

        private void ProcessRelease(Release release)
        {
            var name = release.Name;
            List<DateTime> dates = new List<DateTime>();
            foreach (ProductBacklogItem item in release.ProductBacklogItems)
            {
                foreach (Release linkedRelease in item.LinkedTasks.Where(t => t is Release && IsTeamProject(t)))
                {
                    dates.Add(linkedRelease.Date);
                }
            }
            if (dates.Count == 0)
                return;
            DateTime maxDate = dates.Max();
            if (maxDate != release.Date)
                release.Date = maxDate;
        }

        private void DoUpdate()
        {
            if (initializationOK)
            {
                foreach (ProjectView projectView in projectViews)
                {
                    List<Task> tasks = projectView.Find(find);
                    foreach (Task task in tasks)
                    {
                        if (task is Release)
                            ProcessRelease(task as Release);
                    }
                }
            }
        }

        public override void OnTaskChangeCustomColumnData(TaskChangeCustomColumnDataEventArgs e)
        {
            if (initializationOK)
            {
                Task task = Task.GetTask(e.Data.m_TaskID);
                if (projects.Contains(task.Project) && projectViews.Contains(task.ProjectView))
                {
                    if (task is Release)
                        ProcessRelease(task as Release);
                }
            }
        }
    }
}
