import {ClientError, request} from 'graphql-request';
//import {get} from "./util";

function setIntervalImmediately(func, interval) {
    func();
    return setInterval(func, interval);
}

export async function SingleQuery(query : string, 
                                  parseFn : (data : any) => string) : Promise<any>
{
    const value = await graphQlQuerySingle(window.server, query, parseFn);
    return value;
    //return String(value);
}

export function Query(query : string,
                      parseFn: ( data : any ) => string,
                      invocation : CustomFunctions.StreamingInvocation<string>,
                      continueFn? : ( result : string ) => boolean) : void {
    graphQlQuery(window.server, query, parseFn, window.period, invocation, continueFn);
}

export async function Mutation(mutation : string)
{
    await graphQlMutation(window.server, mutation); 
}


function getGraphQlError(error : ClientError) : string
{
    let response = error.response
    let errors = response.errors
    if (errors)
    {
        let message = errors[0]['extensions']['message']
        return message
    }
    else
        return error.message
}
async function graphQlQuerySingle(server : string,
                      query : string,
                      parseFn: ( data : any ) => string ) : Promise<any>
{
    let result : string = "";
    try {
        result = parseFn(await request(server, query));
    }
    catch(error) {
        result = getGraphQlError(error) 
    }
    return result;
}

async function graphQlMutation(server : string, mutation : string)
{
    try {
        await request(server, mutation);
    }
    catch(error) {
        return getGraphQlError(error)
    }
}

function graphQlQuery(server : string,
                      query : string, 
                                     parseFn: ( data : any ) => string,
                                     period : number,
                                     invocation : CustomFunctions.StreamingInvocation<string>,
                                     continueFn? : ( result : string ) => boolean) : void {
   if (continueFn == undefined)
       continueFn = result => true;
    
    const timer = setIntervalImmediately(() => {
        invocation.setResult("Querying...");
        try {
            request(server, query).then(data => {
                let result = parseFn(data);
                invocation.setResult(result);
                if (!continueFn(result))
                    clearInterval(timer);
                })
                .catch(r => invocation.setResult(r.message))
        }
        catch(error) {
            invocation.setResult(getGraphQlError(error))
        }
    }, period);

    invocation.onCanceled = () => {
        clearInterval(timer);
    };
}
