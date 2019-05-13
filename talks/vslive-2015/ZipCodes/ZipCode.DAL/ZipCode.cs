using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ZipCode.DAL
{
    [Amazon.DynamoDBv2.DataModel.DynamoDBTable(ZipCodeEntity.TableName)]
    public class ZipCodeEntity
    {
        public const string TableName = "ZipCodes";

        public ZipCodeEntity()
        {
        }

        public string PostalCode { get; set; }
        public string CountryCode { get; set; }
        public string PlaceName { get; set; }
        public string State { get; set; }
        public string StateAbbrevation { get; set; }
        public string City { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string UniqueKey
        {
            get
            {
                return string.Format("Country: {0}, Postal Code: {1}", this.CountryCode, this.PostalCode);
            }
        }
    }
}
