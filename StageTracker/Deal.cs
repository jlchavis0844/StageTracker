using System;

namespace StageTracker {
    public class Deal {

        public int deal_id { set; get; }
        public int stage_id { set; get; }
        public string stage_name { set; get; }
        public int owner_id { set; get; }
        public string owner_name { set; get; }
        public int contact_id { set; get; }
        public string ecd { set; get; }
        public DateTime changed_at { set; get; }
        public string event_id { set; get; }
        public string event_type { set; get; }
        public int previous_stage_id { set; get; }
        public string previous_stage_name { set; get; }
        public string position { set; get; }
        public DateTime created_at { set; get; }

        public Deal(int id, int stage_id, string stage_name, int owner_id, string owner_name,
            int contact_id, string ecd, DateTime event_time, string event_id, string event_type,
            int previous_stage_id, string previous_stage_name, string position, DateTime created_at) {
            this.deal_id = id;
            this.stage_id = stage_id;
            this.stage_name = stage_name;
            this.owner_id = owner_id;
            this.owner_name = owner_name;
            this.contact_id = contact_id;
            this.ecd = ecd;
            this.changed_at = event_time;
            this.event_id = event_id;
            this.event_type = event_type;
            this.previous_stage_id = previous_stage_id;
            this.previous_stage_name = previous_stage_name;
            this.position = position;
            this.created_at = created_at;
        }

        public Deal() {

        }
    }
}