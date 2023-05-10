import {Mutation, Query, SingleQuery} from './queries';

class RootInfo {
  rootId : string;
  createdAt: string;
  updatedAt: string;
  numberOfUpdates: number;
  
  constructor(rootId, createdAt, updatedAt, numberOfUpdates) {
    this.rootId = rootId;
    this.createdAt = createdAt;
    this.updatedAt = updatedAt;
    this.numberOfUpdates = Number.parseInt(numberOfUpdates)
  }
}

/**
 * @customfunction
 * @param invocation Custom function handler
 */
export function activeBranch(invocation : CustomFunctions.StreamingInvocation<string>) : void {
  const query = `query { activeBranch }`;

  Query(query, data => data.activeBranch.toString(), invocation);
}

/**
 * @customfunction
 * @param {string} id root id
 */
export async function getOrAddRoot(id : string) : Promise<any> {
  let info = await rootInfo(id);
  if (info != "")
    return info
    
  const query = `mutation { createRoot(name: \"${id}\") }`
  
  let result = await SingleQuery(query, data => data.createRoot.toString())
  window.console.log(result)
  return result
}

/**
 * @customfunction
 * @param {string} id root id
 */
export async function rootInfo(id : string) : Promise<any> {
  const query = `
  query { rootInfoQuery(id: \"${id}\") {
    rootId
    createdAt
    updatedAt
    numberOfUpdates
  }}
  `
  
  let result = await SingleQuery(query, data => data.rootInfoQuery)
  if(!result.rootId)
    return ""
  
  const myEntity: Excel.EntityCellValue = {
    type: Excel.CellValueType.entity,
    text: id,
    properties : {
      "createdAt" : {
        type : Excel.CellValueType.string,
        basicValue : result.createdAt || ""
      },
      "updatedAt" : {
        type: Excel.CellValueType.string,
        basicValue : result.updatedAt || ""
      },
      numberOfUpdates : {
        type: Excel.CellValueType.double,
        basicValue : result.numberOfUpdates 
      }
    },
  }
  return myEntity;
}