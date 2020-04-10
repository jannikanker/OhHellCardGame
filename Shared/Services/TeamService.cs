using BlazorSignalRApp.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSignalRApp.Server.Services
{
    public class TeamService
    {
        private Hashtable _teams;

        private static TeamService instance;

        private TeamService()
        {
            _teams = new Hashtable();
            var g = new Team
            {
                Id = "team1",
                Name = "Team 1",
                Members = new List<Member>()
            };
            _teams.Add(g.Id, g);
        }

        public static TeamService Instance
        {
            get
            {
                if (instance == null)
                    instance = new TeamService();
                return instance;
            }
        }

        public void AddTeam(string teamId, Team team)
        {
            _teams.Add(teamId, team);
        }

        public void AddMember(string teamId, Member member)
        {
            if (teamId != null && _teams.ContainsKey(teamId))
            {
                var team = (Team)_teams[teamId];
                team.Members.Add(member);
            }
        }

        public void RemoveMember(string teamId, string memberId)
        {
            if (teamId != null && _teams.ContainsKey(teamId))
            {
                var team = (Team)_teams[teamId];
                var members = team.Members.Where(m => m.Id == memberId);
                if(members.Count()>0)
                    team.Members.Remove(members.FirstOrDefault());
            }
        }

        public void SetMember(string teamId, string memberId, string emoji)
        {
            if (teamId != null && _teams.ContainsKey(teamId))
            {
                var team = (Team)_teams[teamId];

                var member2Change = team.Members.Find(m => m.Id == memberId);
                if(member2Change != null)
                {
                    var index = team.Members.IndexOf(member2Change);
                    member2Change.Emoji = emoji;
                    team.Members[index] = member2Change;
                }
                else
                {
                    var newMember = new Member { Id = memberId, Name = memberId, Emoji = emoji };
                    team.Members.Add(newMember);
                }
            }
        }

        public Team GetTeam(string key)
        {
            if (key != null && _teams.ContainsKey(key))
            {
                var team = (Team)_teams[key];
                return team;
            }
            return null;
        }

        public List<Team> GetAllTeams()
        {
            var teams = new List<Team>();
            foreach(var g in _teams.Keys)
            {
                teams.Add((Team)_teams[g]);
            }
            return teams;
        }
    }
}
