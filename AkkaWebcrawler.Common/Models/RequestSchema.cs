namespace AkkaWebcrawler.Common.Models
{
    public static class RequestSchema
    {
        public static string Json {
            get {
                return @"
                    {
                        ""$schema"": ""http://json-schema.org/draft-07/schema#"",
                        ""title"": ""JSON Schema for the RequestSchema"",
                        ""type"": ""object"",
                        ""required"": [
                            ""projectId"",
                            ""projectName"",
                            ""domain"",
                            ""startUrls"",
                            ""isFixedListOfUrls"",
                            ""scraperSchemas""
                        ],
                        ""properties"": {
                            ""projectId"": {
                                ""type"": ""string"",
                                ""pattern"": ""^(.*)$""
                            },
                            ""projectName"": {
                                ""type"": ""string"",
                                ""pattern"": ""^(.*)$""
                            },
                            ""domain"": {
                                ""type"": ""string"",
                                ""pattern"": ""^(.*)$""
                            },
                            ""startUrls"": {
                                ""type"": ""array"",
                                ""items"": {
                                    ""type"": ""string"",
                                    ""pattern"": ""^(.*)$""
                                }
                            },
                            ""isFixedListOfUrls"": {
                                ""type"": ""boolean""
                            },
                            ""scraperSchemas"": {
                                ""type"": ""array"",
                                ""items"": {
                                    ""$ref"": ""#/definitions/scraperSchema""
                                },
                                ""minItems"": 1
                            }
                        },
                        ""definitions"": {
                            ""scraperSchema"": {
                                ""type"": ""object"",
                                ""required"": [
                                    ""name"",
                                    ""nodes""
                                ],
                                ""properties"": {
                                    ""name"": {
                                        ""type"": ""string""
                                    },
                                    ""nodes"": {
                                        ""type"": ""array"",
                                        ""items"": {
                                            ""$ref"": ""#/definitions/node""
                                        },
                                        ""minItems"": 1
                                    }
                                }
                            },
                            ""node"": {
                                ""type"": ""object"",
                                ""required"": [
                                    ""name"",
                                    ""type"",
                                    ""getMultipleFromPage"",
                                    ""isRequired"",
                                    ""xpath""
                                ],
                                ""properties"": {
                                    ""name"": {
                                        ""type"": ""string""
                                    },
                                    ""type"": {
                                        ""type"": ""string"",
                                        ""enum"": [
                                            ""string"",
                                            ""number"",
                                            ""boolean"",
                                            ""object""
                                        ]
                                    },
                                    ""getMultipleFromPage"": {
                                        ""type"": ""boolean""
                                    },
                                    ""isRequired"": {
                                        ""type"": ""boolean""
                                    },
                                    ""xpath"": {
                                        ""type"": ""string""
                                    },
                                    ""attributes"": {
                                        ""type"": ""array"",
                                        ""items"": {
                                            ""$ref"": ""#/definitions/node""
                                        },
                                        ""minItems"": 1
                                    }
                                },
                                ""if"": {
                                    ""properties"": {
                                        ""type"": {
                                            ""const"": ""object""
                                        }
                                    }
                                },
                                ""then"": {
                                    ""required"": [
                                        ""attributes""
                                    ]
                                }
                            }
                        }
                    }
                ";
            }
        }
    }
}
